using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SearchAChurch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChurchClaimEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "churches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaceId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FormattedAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClaimStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VerificationTier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_churches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_churches_users_VerifiedByUserId",
                        column: x => x.VerifiedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "church_claims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChurchId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TargetTier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ValidationMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TosAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    TosVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TosAcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReminderSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_church_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_church_claims_churches_ChurchId",
                        column: x => x.ChurchId,
                        principalTable: "churches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_church_claims_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "claim_audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChurchId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ClientPort = table.Column<int>(type: "integer", nullable: false),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    VerificationMetadata = table.Column<string>(type: "jsonb", nullable: false),
                    RetentionUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claim_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_claim_audit_logs_churches_ChurchId",
                        column: x => x.ChurchId,
                        principalTable: "churches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_claim_audit_logs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "dispute_cases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChurchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IncumbentUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeadlineAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChallengerDocumentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IncumbentDocumentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ChallengerAverbationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IncumbentAverbationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispute_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dispute_cases_churches_ChurchId",
                        column: x => x.ChurchId,
                        principalTable: "churches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dispute_cases_users_ChallengerUserId",
                        column: x => x.ChallengerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dispute_cases_users_IncumbentUserId",
                        column: x => x.IncumbentUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "claim_evidences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RawDataOrUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FileHashSha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claim_evidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_claim_evidences_church_claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "church_claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_church_claims_ChurchId_Status",
                table: "church_claims",
                columns: new[] { "ChurchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_church_claims_ExpiresAt",
                table: "church_claims",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_church_claims_UserId",
                table: "church_claims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_churches_ClaimStatus",
                table: "churches",
                column: "ClaimStatus");

            migrationBuilder.CreateIndex(
                name: "IX_churches_Latitude_Longitude",
                table: "churches",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_churches_PlaceId",
                table: "churches",
                column: "PlaceId",
                unique: true,
                filter: "place_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_churches_VerifiedByUserId",
                table: "churches",
                column: "VerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_claim_audit_logs_ChurchId",
                table: "claim_audit_logs",
                column: "ChurchId");

            migrationBuilder.CreateIndex(
                name: "IX_claim_audit_logs_RetentionUntil",
                table: "claim_audit_logs",
                column: "RetentionUntil");

            migrationBuilder.CreateIndex(
                name: "IX_claim_audit_logs_TimestampUtc",
                table: "claim_audit_logs",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_claim_audit_logs_UserId",
                table: "claim_audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_claim_evidences_ClaimId",
                table: "claim_evidences",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_dispute_cases_ChallengerUserId",
                table: "dispute_cases",
                column: "ChallengerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_dispute_cases_ChurchId",
                table: "dispute_cases",
                column: "ChurchId");

            migrationBuilder.CreateIndex(
                name: "IX_dispute_cases_DeadlineAt",
                table: "dispute_cases",
                column: "DeadlineAt");

            migrationBuilder.CreateIndex(
                name: "IX_dispute_cases_IncumbentUserId",
                table: "dispute_cases",
                column: "IncumbentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_dispute_cases_Status",
                table: "dispute_cases",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "claim_audit_logs");

            migrationBuilder.DropTable(
                name: "claim_evidences");

            migrationBuilder.DropTable(
                name: "dispute_cases");

            migrationBuilder.DropTable(
                name: "church_claims");

            migrationBuilder.DropTable(
                name: "churches");
        }
    }
}
