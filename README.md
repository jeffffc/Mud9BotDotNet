# 🤖 Mud9Bot

[![Add to Telegram](https://img.shields.io/badge/Telegram-Add%20to%20Group-26A5E4?style=for-the-badge&logo=telegram)](https://t.me/Mud9Bot)  
![Build Status](https://img.shields.io/github/actions/workflow/status/jeffffc/Mud9BotDotNet/deploy.yml?branch=main&label=CI/CD&style=flat-square) ![Framework](https://img.shields.io/badge/.NET-10.0-512bd4?style=flat-square&logo=dotnet) ![Database](https://img.shields.io/badge/Database-PostgreSQL-336791?style=flat-square&logo=postgresql) ![Last Commit](https://img.shields.io/github/last-commit/jeffffc/Mud9BotDotNet?style=flat-square)

Mud9Bot is an advanced, highly modular Telegram Bot built with C# .NET 10. It features a scalable architecture, dynamic dependency injection, PostgreSQL integration, Quartz.NET scheduling, and a fully automated CI/CD pipeline. It is designed to be easily deployable and extensible for any community.

- - -

## Table of Contents

*   [Architecture & Structure](#architecture)
*   [Getting Started (Local Development)](#getting-started)
*   [Running & Debugging](#debugging)
*   [Adding New Features (Modularity)](#adding-features)
*   [Activating & Deactivating Modules](#activating-modules)
*   [Deployment (CI/CD via GitHub)](#deployment)

- - -

## Architecture & Structure

The bot is designed with a "Convention over Configuration" philosophy. By simply creating a class and adding the right attributes, the system automatically registers it into the Dependency Injection (DI) container.

### Directory Layout

*   `Mud9Bot/Modules/`: Presentation layer. Handles incoming Telegram updates (Commands, Callbacks).
*   `Mud9Bot/Services/`: Business logic layer. Handles API calls and data processing.
*   `Mud9Bot/Data/`: Data access layer. EF Core DbContext and entity definitions.
*   `Mud9Bot/Jobs/`: Scheduled tasks. Background work using Quartz.NET.
*   `Mud9Bot/Registries/`: Core engines. Responsible for scanning reflection and dispatching updates.
*   `Mud9Bot/Attributes/`: Custom attributes (e.g., `[Command]`, `[TextTrigger]`).
*   `Mud9Bot/Interfaces/`: Service interfaces to ensure DI decoupling and testability.

- - -

## Getting Started (Local Development)

### 1\. Prerequisites

*   .NET 10.0 SDK
*   PostgreSQL Server
*   A Telegram Bot Token (from @BotFather)

### 2\. Clone and Configure

Clone the repository and navigate to the project directory. Create an `appsettings.Development.json` file in the `Mud9Bot` folder:

```
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=mud9_db;Username=postgres;Password=your_pw"
  },
  "BotConfiguration": {
    "BotToken": "YOUR_BOT_TOKEN",
    "AdminId": 123456789,
    "DevIds": "123456789, 987654321",
    "LogGroupId": -100123456789
  },
  "GitHub": {
    "Repository": "username/repo",
    "PatToken": "YOUR_GITHUB_PAT"
  }
}
```

### 3\. Database Migration

Mud9Bot uses EF Core Code-First migrations. The database is automatically checked and updated on startup, but you can also execute the command manually:

```
dotnet ef database update
```

- - -

## Running & Debugging

Run the bot locally using the .NET CLI:

```
dotnet run
```

Or press **F5** in your IDE (Visual Studio / JetBrains Rider).

**Debugging Tips:**

*   **Startup Logs**: During startup, `BotExtensions.cs` will list all automatically discovered and registered Commands, Callbacks, and Services in the Console.
*   **Breakpoints**: You can set breakpoints directly in any `Module` or `Service` method.

- - -

## Adding New Features (Modularity)

Thanks to the reflection-based registration, you do not need to manually register new features.

### 1\. Adding a Slash Command (/command)

```
[Command("ping", Description = "Test connection")]
public async Task PingAsync(ITelegramBotClient bot, Message msg, string[] args, CancellationToken ct)
{
    await bot.SendMessage(msg.Chat.Id, "Pong! 🏓", cancellationToken: ct);
}
```

### 2\. Adding a Button Handler (Callback)

Standard format: `PREFIX+TARGET+DATA`.

```
[CallbackQuery("MY_BTN", DevOnly = true)]
public async Task HandleBtnAsync(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
{
    var data = query.Data.Split('+');
    await bot.AnswerCallbackQuery(query.Id, "Click received!", cancellationToken: ct);
}
```

### 3\. Adding a Service

Define an interface (`IMyService`) and a class ending in `Service`. The system will auto-register it.

```
public interface IMyService { void DoWork(); }
public class MyService : IMyService { public void DoWork() { } }
```

- - -

## Activating & Deactivating Modules

*   **Temporary Deactivation**: Add `Inactive = true` to the attribute.
*   **Permission Control**: Use `DevOnly`, `AdminOnly`, `GroupOnly`, or `PrivateOnly`.
*   **Permanent Removal**: Delete the `.cs` file; the route is removed during the next startup scan.

- - -

## Deployment (CI/CD via GitHub)

This project uses a **Symlink deployment strategy** for seamless switching.

### 1\. Server Directory Standards

```
/home/user/mud9bot-deploy/
  ├── current -> (Symlink to latest release folder)
  ├── releases/ -> (Build folders named by Git SHA)
  └── shared/ -> (Production appsettings.json)
```

### 2\. Systemd & Sudoers Configuration

Service file (`mud9bot-prod.service`):

```
WorkingDirectory=/home/user/mud9bot-deploy/current
ExecStart=/usr/bin/dotnet /home/user/mud9bot-deploy/current/Mud9Bot.dll
```

Allow passwordless restart in `sudo visudo`:

```
your_user ALL=(ALL) NOPASSWD: /usr/bin/systemctl restart mud9bot-prod
```

### 3\. GitHub Secrets

*   `SERVER_IP`, `SSH_USERNAME`, `SSH_PRIVATE_KEY`
*   `TELEGRAM_BOT_TOKEN`, `TELEGRAM_LOG_GROUP_ID`
*   `BOT_DEPLOY_PATH`, `BOT_SERVICE_NAME`

### 4\. Triggering Auto-Deployment

1.  Push code to `main`.
2.  Click **Build Bot** in Telegram.
3.  Click **Deploy Bot** once build is ready. GitHub Actions will handle the rest.

[Back to Top](#top)

***

# 🤖 Mud9Bot

Mud9Bot 是一隻採用 C# .NET 10 開發的高性能、高度模組化 Telegram 機器人。具備動態依賴注入 (Dynamic DI)、PostgreSQL 整合、Quartz.NET 排程以及全自動化 CI/CD 流程。

## 目錄

* [🏗️ 系統架構與目錄結構](#structure)
* [🚀 環境架設 (本地開發)](#setup)
* [🐛 運行與除錯](#debugging)
* [🧩 功能擴充 (添加模組)](#modularity)
* [🎛️ 啟用/停用功能](#activation)
* [🚢 部署流程 (CI/CD)](#deployment)


## 🏗️ 系統架構與目錄結構

本機器人遵循「約定優於配置」的開發原則。大多數情況下，您只需建立類別並加上對應 Attribute，系統就會自動完成 DI 註冊與路由綁定。

*   `Mud9Bot/Modules/`: 表現層。處理所有來自 Telegram 的 Update (指令、按鈕)。
*   `Mud9Bot/Services/`: 商業邏輯層。處理 API 呼叫、資料運算。
*   `Mud9Bot/Data/`: 資料存取層。EF Core DbContext 與實體定義。
*   `Mud9Bot/Jobs/`: 定時任務。使用 Quartz.NET 執行背景工作。
*   `Mud9Bot/Registries/`: 核心引擎。負責掃描 Reflection 並分發指令。

## 🚀 環境架設 (本地開發)

### 1\. 準備工具

*   .NET 10.0 SDK
*   PostgreSQL Server
*   Telegram Bot Token (從 @BotFather 取得)

### 2\. 設定檔案

在 `Mud9Bot` 目錄下建立 `appsettings.Development.json`：

```
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=mud9_db;Username=postgres;Password=your_pw"
  },
  "BotConfiguration": {
    "BotToken": "你的_BOT_TOKEN",
    "AdminId": 123456789,
    "DevIds": "123456789, 987654321",
    "LogGroupId": -100123456789
  }
}
```

## 🐛 運行與除錯

使用 CLI 啟動機器人：

```
dotnet run
```

啟動後，Console 會詳列自動偵測並註冊成功的 `Commands`, `Callbacks` 與 `Services`。您可以直接在 Module 中設定斷點 (Breakpoints) 進行邏輯除錯。

## 🧩 功能擴充 (添加模組)

### 添加指令 (/cmd)

```
[Command("ping", Description = "測試連線")]
public async Task PingAsync(ITelegramBotClient bot, Message msg, string[] args, CancellationToken ct) {
    await bot.SendMessage(msg.Chat.Id, "Pong! 🏓", cancellationToken: ct);
}
```

### 添加正則監聽 (Regex)

```
[TextTrigger(@"\b(hello)\b", Description = "打招呼")]
public async Task HelloAsync(ITelegramBotClient bot, Message msg, CancellationToken ct) {
    await bot.Reply(msg, "你好呀！", ct: ct);
}
```

## 🎛️ 啟用/停用功能

暫時停用：

在 Attribute 中加入 `Inactive = true` 即可，系統掃描時會完全跳過該方法。

權限管控：

支援 `DevOnly`, `AdminOnly`, `GroupOnly` 等快速切換。

## 🚢 部署流程 (CI/CD via GitHub)

本項目採用 **Symlink (軟連結) 部署策略**，確保發布過程流暢且易於回滾。

### 1\. 伺服器目錄規範

```
/home/user/mud9bot-deploy/
  ├── current -> (指向最新版本資料夾的捷徑)
  ├── releases/ -> (存放各版本的二進位檔案)
  └── shared/ -> (存放生產環境的 appsettings.json)
```

### 2\. Systemd 服務設定

Service 的 `WorkingDirectory` 與 `ExecStart` 必須指向 `.../current/` 路徑。

```
ExecStart=/usr/bin/dotnet /home/user/mud9bot-deploy/current/Mud9Bot.dll
```

### 3\. GitHub Secrets (必要設定)

SERVER\_IP

伺服器 IP 位置

SSH\_PRIVATE\_KEY

ED25519 或 RSA 私鑰

TELEGRAM\_BOT\_TOKEN

發送部署通知用

BOT\_SERVICE\_NAME

Systemd 服務名稱 (例如 mud9bot-prod)

### 4\. 觸發自動發布

1.  Push Code: 推送至 `main` 分支。
2.  Build: 在 Telegram Log Group 點擊 `🔨 Build Bot`。
3.  Deploy: 編譯成功後點擊 `🚀 Deploy Bot`。
4.  Auto-Swap: GitHub Action 會自動建立新資料夾、複製 `shared` 設定檔、切換 `current` 連結，並重啟服務。

© 2026 Mud9Bot Project. Built with .NET 10 & ❤️ for the community.