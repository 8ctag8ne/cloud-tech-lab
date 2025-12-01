using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenAiRagDemo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBookProcessingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "page_number",
                table: "book_chunks",
                newName: "chunk_index");

            migrationBuilder.AddColumn<bool>(
                name: "is_processed",
                table: "books",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "processed_at",
                table: "books",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "processing_error",
                table: "books",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_processed",
                table: "books");

            migrationBuilder.DropColumn(
                name: "processed_at",
                table: "books");

            migrationBuilder.DropColumn(
                name: "processing_error",
                table: "books");

            migrationBuilder.RenameColumn(
                name: "chunk_index",
                table: "book_chunks",
                newName: "page_number");
        }
    }
}
