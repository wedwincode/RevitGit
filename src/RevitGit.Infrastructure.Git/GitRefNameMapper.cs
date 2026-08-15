using System;
using RevitGit.Domain.Identifiers;

namespace RevitGit.Infrastructure.Git
{
    internal static class GitRefNameMapper
    {
        public static string ForVariant(VariantId variantId)
        {
            if (variantId == null) throw new ArgumentNullException(nameof(variantId));
            return "variant/" + variantId.Value.ToString("N");
        }
    }
}
