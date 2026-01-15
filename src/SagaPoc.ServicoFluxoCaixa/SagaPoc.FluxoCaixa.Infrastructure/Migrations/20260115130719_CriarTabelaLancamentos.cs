using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SagaPoc.FluxoCaixa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CriarTabelaLancamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lancamentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    data_lancamento = table.Column<DateTime>(type: "date", nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    comerciante = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    categoria = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lancamentos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_lancamentos_comerciante_data",
                table: "lancamentos",
                columns: new[] { "comerciante", "data_lancamento" });

            migrationBuilder.CreateIndex(
                name: "idx_lancamentos_data",
                table: "lancamentos",
                column: "data_lancamento");

            migrationBuilder.CreateIndex(
                name: "idx_lancamentos_status",
                table: "lancamentos",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lancamentos");
        }
    }
}
