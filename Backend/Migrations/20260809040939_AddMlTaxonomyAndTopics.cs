using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EduMy.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddMlTaxonomyAndTopics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PrimaryCategoryId",
                table: "Courses",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Topics",
                columns: table => new
                {
                    TopicId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topics", x => x.TopicId);
                });

            migrationBuilder.CreateTable(
                name: "CourseTopics",
                columns: table => new
                {
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    TopicId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseTopics", x => new { x.CourseId, x.TopicId });
                    table.ForeignKey(
                        name: "FK_CourseTopics_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "CourseId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseTopics_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "TopicId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Courses_PrimaryCategoryId",
                table: "Courses",
                column: "PrimaryCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseTopics_TopicId",
                table: "CourseTopics",
                column: "TopicId");

            migrationBuilder.AddForeignKey(
                name: "FK_Courses_Categories_PrimaryCategoryId",
                table: "Courses",
                column: "PrimaryCategoryId",
                principalTable: "Categories",
                principalColumn: "CategoryId",
                onDelete: ReferentialAction.SetNull);

            // Data Migration: Populate PrimaryCategoryId using existing CourseCategories relationship
            migrationBuilder.Sql("UPDATE \"Courses\" c SET \"PrimaryCategoryId\" = (SELECT cc.\"CategoryId\" FROM \"CourseCategories\" cc WHERE cc.\"CourseId\" = c.\"CourseId\" LIMIT 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Courses_Categories_PrimaryCategoryId",
                table: "Courses");

            migrationBuilder.DropTable(
                name: "CourseTopics");

            migrationBuilder.DropTable(
                name: "Topics");

            migrationBuilder.DropIndex(
                name: "IX_Courses_PrimaryCategoryId",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "PrimaryCategoryId",
                table: "Courses");
        }
    }
}
