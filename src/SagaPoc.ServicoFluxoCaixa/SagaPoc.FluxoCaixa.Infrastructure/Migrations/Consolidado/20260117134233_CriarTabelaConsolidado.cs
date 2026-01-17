using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SagaPoc.FluxoCaixa.Infrastructure.Migrations.Consolidado
{
    /// <inheritdoc />
    public partial class CriarTabelaConsolidado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsolidadosDiarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Data = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Comerciante = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TotalCreditos = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalDebitos = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    QuantidadeCreditos = table.Column<int>(type: "integer", nullable: false),
                    QuantidadeDebitos = table.Column<int>(type: "integer", nullable: false),
                    UltimaAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsolidadosDiarios", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Consolidado_Data_Comerciante",
                table: "ConsolidadosDiarios",
                columns: new[] { "Data", "Comerciante" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsolidadosDiarios");
        }
    }
}
