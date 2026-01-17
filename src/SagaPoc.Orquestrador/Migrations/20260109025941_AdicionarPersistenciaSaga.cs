using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SagaPoc.Orquestrador.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarPersistenciaSaga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PedidoSagas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "ID único da instância da SAGA"),
                    Revision = table.Column<int>(type: "integer", nullable: false, comment: "Número de revisão para controle de concorrência otimista"),
                    EstadoAtual = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Estado atual da SAGA"),
                    ClienteId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "ID do cliente que fez o pedido"),
                    RestauranteId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "ID do restaurante onde o pedido foi feito"),
                    ValorTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, comment: "Valor total do pedido"),
                    EnderecoEntrega = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Endereço para entrega do pedido"),
                    FormaPagamento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Forma de pagamento utilizada"),
                    TransacaoId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "ID da transação de pagamento (necessário para estorno)"),
                    EntregadorId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "ID do entregador alocado (necessário para liberação)"),
                    PedidoRestauranteId = table.Column<Guid>(type: "uuid", nullable: true, comment: "ID do pedido no sistema do restaurante"),
                    EmCompensacao = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o pedido está em processo de compensação"),
                    DataInicioCompensacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Timestamp de início da compensação"),
                    DataConclusaoCompensacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Timestamp de conclusão da compensação"),
                    PassosCompensados = table.Column<string>(type: "text", nullable: false, comment: "Lista de passos compensados com sucesso"),
                    RestauranteValidado = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se a validação do restaurante foi executada"),
                    PagamentoProcessado = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o pagamento foi processado"),
                    EntregadorAlocado = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o entregador foi alocado"),
                    TentativasCompensacao = table.Column<int>(type: "integer", nullable: false, comment: "Contador de tentativas de compensação"),
                    ErrosCompensacao = table.Column<string>(type: "text", nullable: false, comment: "Erros ocorridos durante a compensação"),
                    DataInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de início do processamento da SAGA"),
                    DataConclusao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Data e hora de conclusão da SAGA"),
                    TempoPreparoMinutos = table.Column<int>(type: "integer", nullable: false, comment: "Tempo estimado de preparo do pedido em minutos"),
                    TempoEntregaMinutos = table.Column<int>(type: "integer", nullable: false, comment: "Tempo estimado de entrega em minutos"),
                    TaxaEntrega = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, comment: "Taxa de entrega cobrada"),
                    MensagemErro = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Mensagem de erro em caso de falha"),
                    MotivoRejeicao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Motivo da rejeição/cancelamento do pedido")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidoSagas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PedidoSagas_ClienteId",
                table: "PedidoSagas",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidoSagas_DataInicio",
                table: "PedidoSagas",
                column: "DataInicio");

            migrationBuilder.CreateIndex(
                name: "IX_PedidoSagas_EstadoAtual",
                table: "PedidoSagas",
                column: "EstadoAtual");

            migrationBuilder.CreateIndex(
                name: "IX_PedidoSagas_RestauranteId",
                table: "PedidoSagas",
                column: "RestauranteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PedidoSagas");
        }
    }
}
