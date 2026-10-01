using System.ComponentModel.DataAnnotations;
using Sieg.DocumentosFiscais.Dominio.Enumeracoes;

namespace Sieg.DocumentosFiscais.Api.Modelos;

public sealed class ListarDocumentosFiscaisRequisicao : IValidatableObject
{
    public TipoDocumentoFiscal? Tipo { get; init; }

    [StringLength(18, ErrorMessage = "O CNPJ deve possuir no máximo 18 caracteres com máscara.")]
    public string? Cnpj { get; init; }

    [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "A unidade federativa deve possuir duas letras.")]
    public string? UnidadeFederativa { get; init; }

    public DateTimeOffset? DataEmissaoInicial { get; init; }
    public DateTimeOffset? DataEmissaoFinal { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "A página deve ser maior que zero.")]
    public int Pagina { get; init; } = 1;

    [Range(1, 100, ErrorMessage = "O tamanho da página deve estar entre 1 e 100.")]
    public int TamanhoPagina { get; init; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DataEmissaoInicial > DataEmissaoFinal)
        {
            yield return new ValidationResult(
                "A data de emissão inicial não pode ser posterior à data final.",
                [nameof(DataEmissaoInicial), nameof(DataEmissaoFinal)]);
        }
    }
}
