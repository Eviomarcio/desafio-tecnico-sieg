using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class CriarEstruturaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documentos_fiscais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    chave_fiscal = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cnpj_emitente = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    cnpj_destinatario = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    unidade_federativa = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    data_emissao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    hash_conteudo = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    conteudo_xml = table.Column<string>(type: "text", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documentos_fiscais", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "eventos_consumidos",
                columns: table => new
                {
                    id_evento = table.Column<Guid>(type: "uuid", nullable: false),
                    consumidor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    consumido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos_consumidos", x => new { x.id_evento, x.consumidor });
                });

            migrationBuilder.CreateTable(
                name: "eventos_pendentes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    conteudo = table.Column<string>(type: "jsonb", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    publicado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    proxima_tentativa_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    quantidade_tentativas = table.Column<int>(type: "integer", nullable: false),
                    ultimo_erro = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos_pendentes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "resumos_documentos_fiscais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_fiscal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    gerado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resumos_documentos_fiscais", x => x.id);
                    table.ForeignKey(
                        name: "FK_resumos_documentos_fiscais_documentos_fiscais_documento_fis~",
                        column: x => x.documento_fiscal_id,
                        principalTable: "documentos_fiscais",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documentos_fiscais_cnpj_destinatario",
                table: "documentos_fiscais",
                column: "cnpj_destinatario");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_fiscais_cnpj_emitente",
                table: "documentos_fiscais",
                column: "cnpj_emitente");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_fiscais_data_emissao",
                table: "documentos_fiscais",
                column: "data_emissao");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_fiscais_unidade_federativa",
                table: "documentos_fiscais",
                column: "unidade_federativa");

            migrationBuilder.CreateIndex(
                name: "ux_documentos_fiscais_hash_conteudo",
                table: "documentos_fiscais",
                column: "hash_conteudo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_documentos_fiscais_tipo_chave_fiscal",
                table: "documentos_fiscais",
                columns: new[] { "tipo", "chave_fiscal" },
                unique: true,
                filter: "chave_fiscal IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_pendentes_publicacao",
                table: "eventos_pendentes",
                columns: new[] { "publicado_em", "proxima_tentativa_em" });

            migrationBuilder.CreateIndex(
                name: "ux_resumos_documentos_fiscais_documento",
                table: "resumos_documentos_fiscais",
                column: "documento_fiscal_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eventos_consumidos");

            migrationBuilder.DropTable(
                name: "eventos_pendentes");

            migrationBuilder.DropTable(
                name: "resumos_documentos_fiscais");

            migrationBuilder.DropTable(
                name: "documentos_fiscais");
        }
    }
}
