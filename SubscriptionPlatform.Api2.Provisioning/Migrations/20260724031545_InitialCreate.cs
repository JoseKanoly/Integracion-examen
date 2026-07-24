using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubscriptionPlatform.Api2.Provisioning.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoursePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsUnlocked = table.Column<bool>(type: "boolean", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoursePermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeadLetterMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MessageBody = table.Column<string>(type: "text", nullable: false),
                    ErrorReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeadLetterMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserAccesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    HasPremiumAccess = table.Column<bool>(type: "boolean", nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccesses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_course_permission_user_course",
                table: "CoursePermissions",
                columns: new[] { "UserId", "CourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_course_permission_userid",
                table: "CoursePermissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "idx_dlm_received_at",
                table: "DeadLetterMessages",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "idx_user_access_email",
                table: "UserAccesses",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "idx_user_access_userid",
                table: "UserAccesses",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoursePermissions");

            migrationBuilder.DropTable(
                name: "DeadLetterMessages");

            migrationBuilder.DropTable(
                name: "UserAccesses");
        }
    }
}
