using System;
using System.Globalization;

namespace MHDoctor
{
    /// <summary>
    /// Localizacao minima (PT-BR / EN) sem arquivos de recurso.
    /// Pode ser forçada com a variavel de ambiente MHDOCTOR_LANG=en
    /// </summary>
    internal static class L
    {
        private static bool? _forcedPt;

        public static bool Pt
        {
            get
            {
                if (_forcedPt.HasValue) return _forcedPt.Value;

                string env = Environment.GetEnvironmentVariable("MHDOCTOR_LANG");
                if (!string.IsNullOrEmpty(env))
                {
                    _forcedPt = env.StartsWith("pt", StringComparison.OrdinalIgnoreCase);
                    return _forcedPt.Value;
                }

                _forcedPt = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
                    .Equals("pt", StringComparison.OrdinalIgnoreCase);
                return _forcedPt.Value;
            }
        }

        /// <summary>Retorna o texto em portugues ou ingles conforme a cultura atual.</summary>
        public static string S(string pt, string en)
        {
            return Pt ? pt : en;
        }
    }
}
