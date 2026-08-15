using RevitGit.Domain.Identifiers;

namespace RevitGit.Domain.Exceptions
{
    public sealed class UnknownVariantException : DomainException
    {
        public UnknownVariantException(VariantId variantId)
            : base(variantId == null
                ? "Variant identifier is required."
                : "Variant does not exist: " + variantId + ".")
        {
            VariantId = variantId;
        }

        public VariantId VariantId { get; }
    }
}
