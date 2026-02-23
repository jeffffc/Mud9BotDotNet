using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Extensions;
using Mud9Bot.Interfaces;
using Mud9Bot.Jobs;
using Quartz;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Mud9Bot.Services;

public class ReminderService(
    IServiceScopeFactory scopeFactory, 
    ISchedulerFactory schedulerFactory,
    ILogger<ReminderService> logger) : IReminderService
{
    private static readonly TimeZoneInfo HkTimeZone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
    private static readonly DateTime MaxSupportedUtc = new(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc);
    private const int MaxRemindersPerUser = 30;

    public ReminderRequest? ParseReminder(string text)
    {
        var nowUtc = DateTime.UtcNow;
        var nowHk = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, HkTimeZone);
        
        // --- 1. 相對時間處理 (支援 秒/分鐘/鐘/日/個月/年) ---
        // 範例：10個月後提我、2年後提我換車
        var relativeMatch = Regex.Match(text, @"^(\d+)\s*(秒|分鐘|個?鐘|日|個月|年)後提我(.+)", RegexOptions.IgnoreCase);
        if (relativeMatch.Success)
        {
            int value = int.Parse(relativeMatch.Groups[1].Value);
            string unit = relativeMatch.Groups[2].Value;
            string content = relativeMatch.Groups[3].Value.Trim();

            DateTime targetUtc;
            try 
            {
                targetUtc = unit switch
                {
                    "秒" => nowUtc.AddSeconds(value),
                    "分鐘" => nowUtc.AddMinutes(value),
                    "個鐘" or "鐘" => nowUtc.AddHours(value),
                    "日" => nowUtc.AddDays(value),
                    "個月" => nowUtc.AddMonths(value),
                    "年" => nowUtc.AddYears(value),
                    _ => nowUtc
                };
            }
            catch (ArgumentOutOfRangeException)
            {
                targetUtc = MaxSupportedUtc;
            }

            if (targetUtc > MaxSupportedUtc) targetUtc = MaxSupportedUtc;

            var targetHkDisplay = TimeZoneInfo.ConvertTimeFromUtc(targetUtc, HkTimeZone);
            return new ReminderRequest(targetUtc, content, $"{value}{unit}後 ({targetHkDisplay:yyyy/MM/dd HH:mm})");
        }

        // --- 2. 複雜日期/時間與重複性質處理 ---
        // 日期格式：YYYYMMDD 或 YYYY/MM/DD 或 YYYY-MM-DD (必須 8 位數字)
        // 時間格式：HHmm (4 位數字) 或 HH:mm
        var complexPattern = @"^(?:(今日|每日|逢?每?日|聽日|後日|[每逢下]?星期[一二三四五六日天]|(?:\d{4}[-/.]?\d{2}[-/.]?\d{2}))\s*)?(?:(\d{1,2})(?:[:.]?(\d{2}))?\s*[點時])?\s*(?:(\d{2})[:.]?(\d{2}))?\s*提我(.+)";
        var m = Regex.Match(text, complexPattern, RegexOptions.IgnoreCase);

        if (m.Success && (!string.IsNullOrEmpty(m.Groups[1].Value) || !string.IsNullOrEmpty(m.Groups[2].Value) || !string.IsNullOrEmpty(m.Groups[4].Value)))
        {
            string datePart = m.Groups[1].Value;
            string hourPart = !string.IsNullOrEmpty(m.Groups[2].Value) ? m.Groups[2].Value : m.Groups[4].Value;
            string minPart = !string.IsNullOrEmpty(m.Groups[3].Value) ? m.Groups[3].Value : m.Groups[5].Value;
            string content = m.Groups[6].Value.Trim();

            DateTime targetHk = nowHk.Date;
            bool dateProcessed = false;
            string? recurrence = null;

            if (datePart.Contains("日") && (datePart.StartsWith("每") || datePart.StartsWith("逢")))
            {
                recurrence = "DAILY";
                dateProcessed = true;
            }
            else if (datePart.Contains("星期") || datePart.Contains("禮拜"))
            {
                targetHk = CalculateNextWeekday(nowHk, datePart);
                if (datePart.StartsWith("逢") || datePart.StartsWith("每")) 
                {
                    recurrence = GetDayOfWeekShort(datePart);
                }
                dateProcessed = true;
            }
            else if (datePart == "聽日")
            {
                targetHk = targetHk.AddDays(1);
                dateProcessed = true;
            }
            else if (datePart == "後日")
            {
                targetHk = targetHk.AddDays(2);
                dateProcessed = true;
            }
            else if (datePart == "今日")
            {
                dateProcessed = true;
            }
            else if (!string.IsNullOrEmpty(datePart) && Regex.IsMatch(datePart, @"\d"))
            {
                if (TryParseFlexibleDate(datePart, nowHk, out var parsedDate))
                {
                    targetHk = parsedDate;
                    dateProcessed = true;
                }
            }

            if (!string.IsNullOrEmpty(hourPart))
            {
                int h = int.Parse(hourPart);
                int min = !string.IsNullOrEmpty(minPart) ? int.Parse(minPart) : 0;
                
                targetHk = targetHk.AddHours(h).AddMinutes(min);
                
                if ((!dateProcessed || datePart == "今日") && targetHk < nowHk)
                {
                    targetHk = targetHk.AddDays(1);
                }
            }
            else
            {
                targetHk = targetHk.AddHours(nowHk.Hour).AddMinutes(nowHk.Minute);
                if ((!dateProcessed || datePart == "今日") && targetHk <= nowHk)
                {
                    targetHk = targetHk.AddDays(1);
                }
            }

            var targetUtc = TimeZoneInfo.ConvertTimeToUtc(targetHk, HkTimeZone);
            if (targetUtc > MaxSupportedUtc) targetUtc = MaxSupportedUtc;

            return new ReminderRequest(targetUtc, content, recurrence != null ? $"重複 ({datePart} {targetHk:HH:mm})" : targetHk.ToString("yyyy/MM/dd HH:mm"), recurrence);
        }

        return null;
    }

    public async Task CreateReminderAsync(long chatId, long userId, string userName, int msgId, ReminderRequest request)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        int activeCount = await db.Set<Job>().CountAsync(j => j.TelegramId == userId && !j.IsProcessed);
        if (activeCount >= MaxRemindersPerUser)
        {
            throw new InvalidOperationException($"你已經有 {MaxRemindersPerUser} 個生效中嘅提醒喇，刪咗啲舊野先再加啦！");
        }

        var jobRecord = new Job
        {
            ChatId = chatId,
            TelegramId = userId,
            Name = userName,
            MessageId = msgId,
            Time = request.RemindTime, 
            Text = request.Content,
            TimeAdded = DateTime.UtcNow,
            IsProcessed = false,
            Recurrence = request.Recurrence
        };

        db.Set<Job>().Add(jobRecord);
        await db.SaveChangesAsync();

        await ScheduleQuartzJobAsync(jobRecord);
    }

    public async Task RecoverPendingRemindersAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        var nowUtc = DateTime.UtcNow;
        var pending = await db.Set<Job>()
            .Where(j => !j.IsProcessed && (j.Time > nowUtc || j.Recurrence != null))
            .ToListAsync();

        foreach (var j in pending)
        {
            await ScheduleQuartzJobAsync(j);
        }
        
        logger.LogInformation("Recovered {Count} future/recurring reminders.", pending.Count);
    }

    public async Task<bool> DeleteReminderAsync(int jobId, long userId)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        var job = await db.Set<Job>().FirstOrDefaultAsync(j => j.JobId == jobId && j.TelegramId == userId);
        if (job == null) return false;

        job.IsProcessed = true; 
        await db.SaveChangesAsync();

        var scheduler = await schedulerFactory.GetScheduler();
        await scheduler.UnscheduleJob(new TriggerKey($"trigger_{jobId}", "reminders"));
        await scheduler.DeleteJob(new JobKey($"reminder_{jobId}", "reminders"));

        return true;
    }

    private async Task ScheduleQuartzJobAsync(Job jobRecord)
    {
        var scheduler = await schedulerFactory.GetScheduler();
        var utcTime = DateTime.SpecifyKind(jobRecord.Time, DateTimeKind.Utc);
        var hkTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, HkTimeZone);

        var job = JobBuilder.Create<ExecuteReminderJob>()
            .WithIdentity($"reminder_{jobRecord.JobId}", "reminders")
            .UsingJobData("jobId", jobRecord.JobId)
            .Build();

        TriggerBuilder triggerBuilder = TriggerBuilder.Create()
            .WithIdentity($"trigger_{jobRecord.JobId}", "reminders");

        if (string.IsNullOrEmpty(jobRecord.Recurrence))
        {
            triggerBuilder.StartAt(new DateTimeOffset(utcTime));
        }
        else
        {
            string cron = jobRecord.Recurrence == "DAILY" 
                ? $"0 {hkTime.Minute} {hkTime.Hour} * * ?" 
                : $"0 {hkTime.Minute} {hkTime.Hour} ? * {jobRecord.Recurrence}";
            
            triggerBuilder.WithCronSchedule(cron, x => x.InTimeZone(HkTimeZone));
        }

        await scheduler.ScheduleJob(job, triggerBuilder.Build());
    }

    private DateTime CalculateNextWeekday(DateTime nowHk, string input)
    {
        int targetDay = input.Last() switch
        {
            '一' => 1, '二' => 2, '三' => 3, '四' => 4, '五' => 5, '六' => 6, 
            '日' or '天' => 0,
            _ => (int)nowHk.DayOfWeek
        };

        int currentDay = (int)nowHk.DayOfWeek;
        int daysUntil = (targetDay - currentDay + 7) % 7;
        if (daysUntil == 0) daysUntil = 7;
        if (input.StartsWith("下")) daysUntil += 7;

        return nowHk.Date.AddDays(daysUntil);
    }

    private string GetDayOfWeekShort(string input) => input.Last() switch
    {
        '一' => "MON", '二' => "TUE", '三' => "WED", '四' => "THU", '五' => "FRI", '六' => "SAT", 
        _ => "SUN"
    };

    private bool TryParseFlexibleDate(string input, DateTime nowHk, out DateTime result)
    {
        string clean = Regex.Replace(input, @"[-/.]", "");
        string[] formats = { "yyyyMMdd", "ddMMyyyy" };

        foreach (var fmt in formats)
        {
            if (DateTime.TryParseExact(clean, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                result = dt;
                return true;
            }
        }
        result = DateTime.MinValue;
        return false;
    }
}