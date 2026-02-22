using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mud9Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "command_logs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    Command = table.Column<string>(type: "text", nullable: false),
                    Args = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_command_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "currency_rates",
                columns: table => new
                {
                    code = table.Column<string>(type: "text", nullable: false),
                    rate = table.Column<double>(type: "double precision", nullable: false),
                    rate_hkd = table.Column<double>(type: "double precision", nullable: false),
                    last_updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currency_rates", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "custom_greetings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    telegram_id = table.Column<long>(type: "bigint", nullable: false),
                    greeting_type = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_greetings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dailylimit",
                columns: table => new
                {
                    limitid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<int>(type: "integer", nullable: false),
                    groupid = table.Column<int>(type: "integer", nullable: false),
                    wlimit = table.Column<int>(type: "integer", nullable: false),
                    plimit = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dailylimit", x => x.limitid);
                });

            migrationBuilder.CreateTable(
                name: "donation",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    donationid = table.Column<string>(type: "text", nullable: true),
                    telegramid = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: true),
                    username = table.Column<string>(type: "text", nullable: true),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    stars = table.Column<int>(type: "integer", nullable: false),
                    time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    telegram_payment_charge_id = table.Column<string>(type: "text", nullable: true),
                    provider_payment_charge_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_donation", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fortune_limit",
                columns: table => new
                {
                    ftid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    groupid = table.Column<int>(type: "integer", nullable: false),
                    userid = table.Column<int>(type: "integer", nullable: false),
                    last_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    msgid = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fortune_limit", x => x.ftid);
                });

            migrationBuilder.CreateTable(
                name: "gaygif",
                columns: table => new
                {
                    gaygifid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fileid = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gaygif", x => x.gaygifid);
                });

            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    groupid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    telegramid = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    username = table.Column<string>(type: "text", nullable: true),
                    wquota = table.Column<int>(type: "integer", nullable: false),
                    pquota = table.Column<int>(type: "integer", nullable: false),
                    welcome = table.Column<long>(type: "bigint", nullable: false),
                    welcomegif = table.Column<string>(type: "text", nullable: true),
                    pinned_id = table.Column<int>(type: "integer", nullable: false),
                    timeadded = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    offfortune = table.Column<bool>(type: "boolean", nullable: false),
                    offzodiac = table.Column<bool>(type: "boolean", nullable: false),
                    offlomo = table.Column<bool>(type: "boolean", nullable: false),
                    offsimp = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.groupid);
                });

            migrationBuilder.CreateTable(
                name: "job",
                columns: table => new
                {
                    jobid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    timeadded = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    chatid = table.Column<long>(type: "bigint", nullable: false),
                    telegramid = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: true),
                    msgid = table.Column<int>(type: "integer", nullable: false),
                    time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    text = table.Column<string>(type: "text", nullable: true),
                    is_processed = table.Column<bool>(type: "boolean", nullable: false),
                    recurrence = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job", x => x.jobid);
                });

            migrationBuilder.CreateTable(
                name: "lomo_ignore_words",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    word = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lomo_ignore_words", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "movies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Rating = table.Column<string>(type: "text", nullable: false),
                    Link = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Genre = table.Column<string>(type: "text", nullable: false),
                    Writer = table.Column<string>(type: "text", nullable: false),
                    Director = table.Column<string>(type: "text", nullable: false),
                    Starring = table.Column<string>(type: "text", nullable: false),
                    Length = table.Column<string>(type: "text", nullable: false),
                    Grade = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    on_show_date = table.Column<string>(type: "text", nullable: false),
                    is_showing = table.Column<bool>(type: "boolean", nullable: false),
                    last_updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    userid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    telegramid = table.Column<long>(type: "bigint", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: true),
                    username = table.Column<string>(type: "text", nullable: true),
                    plastic = table.Column<int>(type: "integer", nullable: false),
                    wine = table.Column<int>(type: "integer", nullable: false),
                    timeadded = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FirstSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.userid);
                });

            migrationBuilder.CreateTable(
                name: "wineplastic",
                columns: table => new
                {
                    wpid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    groupid = table.Column<int>(type: "integer", nullable: false),
                    userid = table.Column<int>(type: "integer", nullable: false),
                    wine = table.Column<int>(type: "integer", nullable: false),
                    plastic = table.Column<int>(type: "integer", nullable: false),
                    givenby = table.Column<int>(type: "integer", nullable: false),
                    timeadded = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    disabled = table.Column<int>(type: "integer", nullable: false),
                    disableddate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wineplastic", x => x.wpid);
                });

            migrationBuilder.CreateTable(
                name: "zodiacs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    date_key = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    zodiac_index = table.Column<int>(type: "integer", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    OverallScore = table.Column<int>(type: "integer", nullable: false),
                    OverallText = table.Column<string>(type: "text", nullable: false),
                    LoveScore = table.Column<int>(type: "integer", nullable: false),
                    LoveText = table.Column<string>(type: "text", nullable: false),
                    CareerScore = table.Column<int>(type: "integer", nullable: false),
                    CareerText = table.Column<string>(type: "text", nullable: false),
                    MoneyScore = table.Column<int>(type: "integer", nullable: false),
                    MoneyText = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zodiacs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_groups_telegramid",
                table: "groups",
                column: "telegramid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_telegramid",
                table: "users",
                column: "telegramid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "command_logs");

            migrationBuilder.DropTable(
                name: "currency_rates");

            migrationBuilder.DropTable(
                name: "custom_greetings");

            migrationBuilder.DropTable(
                name: "dailylimit");

            migrationBuilder.DropTable(
                name: "donation");

            migrationBuilder.DropTable(
                name: "fortune_limit");

            migrationBuilder.DropTable(
                name: "gaygif");

            migrationBuilder.DropTable(
                name: "groups");

            migrationBuilder.DropTable(
                name: "job");

            migrationBuilder.DropTable(
                name: "lomo_ignore_words");

            migrationBuilder.DropTable(
                name: "movies");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "wineplastic");

            migrationBuilder.DropTable(
                name: "zodiacs");
        }
    }
}
