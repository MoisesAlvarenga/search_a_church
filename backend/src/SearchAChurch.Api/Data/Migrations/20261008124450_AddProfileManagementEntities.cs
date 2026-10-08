using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SearchAChurch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileManagementEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyStamp",
                table: "churches",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Denomination",
                table: "churches",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "churches",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "churches",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Languages",
                table: "churches",
                type: "jsonb",
                nullable: false,
                defaultValue: "[\"pt\"]");

            migrationBuilder.AddColumn<string>(
                name: "SocialFacebook",
                table: "churches",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocialInstagram",
                table: "churches",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorshipStyle",
                table: "churches",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "church_meeting_schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChurchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "pt"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_church_meeting_schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_church_meeting_schedules_churches_ChurchId",
                        column: x => x.ChurchId,
                        principalTable: "churches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tag_catalogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IconName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tag_catalogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Denomination = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WorshipStyle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PreferredLanguages = table.Column<string>(type: "jsonb", nullable: false),
                    DefaultRadiusKm = table.Column<double>(type: "double precision", nullable: false, defaultValue: 10.0),
                    IsAnonymous = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_profiles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "church_tags",
                columns: table => new
                {
                    ChurchId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_church_tags", x => new { x.ChurchId, x.TagId });
                    table.ForeignKey(
                        name: "FK_church_tags_churches_ChurchId",
                        column: x => x.ChurchId,
                        principalTable: "churches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_church_tags_tag_catalogs_TagId",
                        column: x => x.TagId,
                        principalTable: "tag_catalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_profile_tags",
                columns: table => new
                {
                    UserProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profile_tags", x => new { x.UserProfileId, x.TagId });
                    table.ForeignKey(
                        name: "FK_user_profile_tags_tag_catalogs_TagId",
                        column: x => x.TagId,
                        principalTable: "tag_catalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_profile_tags_user_profiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "user_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "tag_catalogs",
                columns: new[] { "Id", "Category", "Code", "CreatedAt", "Description", "DisplayOrder", "IconName", "IsActive", "Name" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0001-000000000001"), 1, "rampa_acesso", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Acesso pleno a cadeirantes e pessoas com mobilidade reduzida.", 1, "accessible", true, "Rampa de Acesso" },
                    { new Guid("00000000-0000-0000-0001-000000000002"), 1, "interprete_libras", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Inclusão de surdos e deficientes auditivos durante o culto.", 2, "sign_language", true, "Intérprete de LIBRAS" },
                    { new Guid("00000000-0000-0000-0001-000000000003"), 1, "banheiro_acessivel", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Instalações sanitárias projetadas para acessibilidade.", 3, "wc", true, "Banheiro Adaptado" },
                    { new Guid("00000000-0000-0000-0001-000000000004"), 1, "elevador_acessivel", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Acesso facilitado entre diferentes andares do templo.", 4, "elevator", true, "Elevador / Plataforma" },
                    { new Guid("00000000-0000-0000-0001-000000000005"), 1, "audiodescricao", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Apoio para pessoas com deficiência visual.", 5, "hearing", true, "Recurso de Audiodescrição" },
                    { new Guid("00000000-0000-0000-0002-000000000001"), 2, "estacionamento_proprio", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Vagas gratuitas ou privativas para fiéis e visitantes.", 6, "local_parking", true, "Estacionamento Próprio" },
                    { new Guid("00000000-0000-0000-0002-000000000002"), 2, "ar_condicionado", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Templo totalmente equipado com climatização.", 7, "ac_unit", true, "Ambiente Climatizado" },
                    { new Guid("00000000-0000-0000-0002-000000000003"), 2, "espaco_kids_bercario", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Sala dedicada a bebês e crianças pequenas com monitoria.", 8, "child_care", true, "Espaço Kids & Berçário" },
                    { new Guid("00000000-0000-0000-0002-000000000004"), 2, "transmissao_online", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Transmissão em tempo real via YouTube, Facebook ou site.", 9, "live_tv", true, "Transmissão Ao Vivo" },
                    { new Guid("00000000-0000-0000-0002-000000000005"), 2, "refeitorio_cantina", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Espaço comunitário para comunhão e alimentação pós-culto.", 10, "restaurant", true, "Refeitório / Cantina" },
                    { new Guid("00000000-0000-0000-0003-000000000001"), 3, "ministerio_jovens", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Reuniões, células e eventos focados no público jovem.", 11, "groups", true, "Ministério de Jovens" },
                    { new Guid("00000000-0000-0000-0003-000000000002"), 3, "ministerio_infantil", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Culto infantil paralelo e atividades pedagógicas.", 12, "child_friendly", true, "Ministério Infantil" },
                    { new Guid("00000000-0000-0000-0003-000000000003"), 3, "ministerio_casais", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cursos, encontros e acompanhamento para famílias.", 13, "favorite", true, "Ministério de Casais" },
                    { new Guid("00000000-0000-0000-0003-000000000004"), 3, "escola_biblica", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Aulas sistemáticas de estudo das Escrituras aos domingos.", 14, "menu_book", true, "Escola Bíblica (EBD)" },
                    { new Guid("00000000-0000-0000-0003-000000000005"), 3, "acao_social", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Distribuição de cestas básicas, roupas e amparo a vulneráveis.", 15, "volunteer_activism", true, "Ação Social Comunitária" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_churches_IsActive",
                table: "churches",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_church_meeting_schedules_ChurchId",
                table: "church_meeting_schedules",
                column: "ChurchId");

            migrationBuilder.CreateIndex(
                name: "IX_church_meeting_schedules_DayOfWeek",
                table: "church_meeting_schedules",
                column: "DayOfWeek");

            migrationBuilder.CreateIndex(
                name: "IX_church_tags_TagId",
                table: "church_tags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_tag_catalogs_Category",
                table: "tag_catalogs",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_tag_catalogs_Code",
                table: "tag_catalogs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tag_catalogs_IsActive",
                table: "tag_catalogs",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_user_profile_tags_TagId",
                table: "user_profile_tags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_user_profiles_UserId",
                table: "user_profiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "church_meeting_schedules");

            migrationBuilder.DropTable(
                name: "church_tags");

            migrationBuilder.DropTable(
                name: "user_profile_tags");

            migrationBuilder.DropTable(
                name: "tag_catalogs");

            migrationBuilder.DropTable(
                name: "user_profiles");

            migrationBuilder.DropIndex(
                name: "IX_churches_IsActive",
                table: "churches");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "churches");

            migrationBuilder.DropColumn(
                name: "Denomination",
                table: "churches");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "churches");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "churches");

            migrationBuilder.DropColumn(
                name: "Languages",
                table: "churches");

            migrationBuilder.DropColumn(
                name: "SocialFacebook",
                table: "churches");

            migrationBuilder.DropColumn(
                name: "SocialInstagram",
                table: "churches");

            migrationBuilder.DropColumn(
                name: "WorshipStyle",
                table: "churches");
        }
    }
}
