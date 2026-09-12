using System;

namespace WebApplication1.Services
{
    public static class CurrencyWordConverter
    {
        private static readonly string[] EnglishUnits = {
            "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine",
            "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"
        };

        private static readonly string[] EnglishTens = {
            "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"
        };

        public static string ToEnglishWords(decimal amount, string currencyName = "Jordanian Dinars", string fractionName = "Piastres")
        {
            if (amount < 0)
                return "Minus " + ToEnglishWords(Math.Abs(amount), currencyName, fractionName);

            long wholePart = (long)Math.Floor(amount);
            int fractionPart = (int)Math.Round((amount - wholePart) * 100);

            string words = ConvertWholeNumberEnglish(wholePart);
            if (string.IsNullOrWhiteSpace(words)) words = "Zero";

            string result = $"{words} {currencyName}";

            if (fractionPart > 0)
            {
                string fracWords = ConvertWholeNumberEnglish(fractionPart);
                result += $" and {fracWords} {fractionName}";
            }

            return $"Only {result}";
        }

        private static string ConvertWholeNumberEnglish(long number)
        {
            if (number == 0) return "";
            if (number < 20) return EnglishUnits[number];
            if (number < 100)
            {
                int rem = (int)(number % 10);
                return EnglishTens[number / 10] + (rem > 0 ? "-" + EnglishUnits[rem] : "");
            }
            if (number < 1000)
            {
                long rem = number % 100;
                return EnglishUnits[number / 100] + " Hundred" + (rem > 0 ? " " + ConvertWholeNumberEnglish(rem) : "");
            }
            if (number < 1_000_000)
            {
                long rem = number % 1000;
                return ConvertWholeNumberEnglish(number / 1000) + " Thousand" + (rem > 0 ? " " + ConvertWholeNumberEnglish(rem) : "");
            }
            if (number < 1_000_000_000)
            {
                long rem = number % 1_000_000;
                return ConvertWholeNumberEnglish(number / 1_000_000) + " Million" + (rem > 0 ? " " + ConvertWholeNumberEnglish(rem) : "");
            }

            return number.ToString();
        }

        // ── Arabic Number to Words (Tafqeet) ─────────────────────────────────
        private static readonly string[] ArabicOnes = {
            "", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة",
            "عشرة", "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر", "ستة عشر",
            "سبعة عشر", "ثمانية عشر", "تسعة عشر"
        };

        private static readonly string[] ArabicTens = {
            "", "", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون"
        };

        private static readonly string[] ArabicHundreds = {
            "", "مائة", "مئتان", "ثلاثمائة", "أربعمائة", "خمسمائة", "ستمائة", "سبعمائة", "ثمانمائة", "تسعمائة"
        };

        public static string ToArabicWords(decimal amount, string currencyName = "دينار أردني", string fractionName = "قرش")
        {
            if (amount < 0)
                return "سالب " + ToArabicWords(Math.Abs(amount), currencyName, fractionName);

            long wholePart = (long)Math.Floor(amount);
            int fractionPart = (int)Math.Round((amount - wholePart) * 100);

            if (wholePart == 0 && fractionPart == 0)
                return $"صفر {currencyName} لا غير";

            string wholeStr = ConvertWholeNumberArabic(wholePart);
            string result = "";

            if (wholePart > 0)
            {
                result = $"{wholeStr} {currencyName}";
            }

            if (fractionPart > 0)
            {
                string fracStr = ConvertWholeNumberArabic(fractionPart);
                if (!string.IsNullOrEmpty(result))
                    result += $" و {fracStr} {fractionName}";
                else
                    result = $"{fracStr} {fractionName}";
            }

            return $"فقط {result} لا غير";
        }

        private static string ConvertWholeNumberArabic(long number)
        {
            if (number == 0) return "";
            if (number < 20) return ArabicOnes[number];
            if (number < 100)
            {
                long tens = number / 10;
                long ones = number % 10;
                if (ones == 0) return ArabicTens[tens];
                return ArabicOnes[ones] + " و" + ArabicTens[tens];
            }
            if (number < 1000)
            {
                long hundreds = number / 100;
                long rem = number % 100;
                if (rem == 0) return ArabicHundreds[hundreds];
                return ArabicHundreds[hundreds] + " و" + ConvertWholeNumberArabic(rem);
            }
            if (number < 1_000_000)
            {
                long thousands = number / 1000;
                long rem = number % 1000;
                string thStr;
                if (thousands == 1) thStr = "ألف";
                else if (thousands == 2) thStr = "ألفان";
                else if (thousands >= 3 && thousands <= 10) thStr = ConvertWholeNumberArabic(thousands) + " آلاف";
                else thStr = ConvertWholeNumberArabic(thousands) + " ألف";

                if (rem == 0) return thStr;
                return thStr + " و" + ConvertWholeNumberArabic(rem);
            }
            if (number < 1_000_000_000)
            {
                long millions = number / 1_000_000;
                long rem = number % 1_000_000;
                string milStr;
                if (millions == 1) milStr = "مليون";
                else if (millions == 2) milStr = "مليونان";
                else if (millions >= 3 && millions <= 10) milStr = ConvertWholeNumberArabic(millions) + " ملايين";
                else milStr = ConvertWholeNumberArabic(millions) + " مليون";

                if (rem == 0) return milStr;
                return milStr + " و" + ConvertWholeNumberArabic(rem);
            }

            return number.ToString();
        }
    }
}
