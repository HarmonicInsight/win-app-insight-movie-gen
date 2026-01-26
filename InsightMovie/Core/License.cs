namespace InsightMovie.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

public enum PlanCode
{
    Free,
    Std,
    Pro,
    Ent
}

public class LicenseInfo
{
    public string? ProductCode { get; set; }
    public PlanCode Plan { get; set; }
    public string? YearMonth { get; set; }
    public bool IsValid { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public static class License
{
    public const string PRODUCT_CODE = "INMV";
    public const string SECRET_KEY = "insight-series-license-secret-2026";

    private static readonly Regex LICENSE_KEY_REGEX = new(
        @"^(INSS|INSP|INPY|FGIN|INMV)-(STD|PRO|ENT)-(\d{4})-([A-Z0-9]{4})-([A-Z0-9]{4})-([A-Z0-9]{4})$",
        RegexOptions.Compiled);

    private static readonly Dictionary<string, PlanCode[]> FEATURE_MATRIX = new()
    {
        { "subtitle", new[] { PlanCode.Pro, PlanCode.Ent } },
        { "subtitle_style", new[] { PlanCode.Pro, PlanCode.Ent } },
        { "transition", new[] { PlanCode.Pro, PlanCode.Ent } },
        { "pptx_import", new[] { PlanCode.Pro, PlanCode.Ent } },
    };

    public static string GenerateSignature(string product, string plan, string yymm)
    {
        var raw = $"{product}-{plan}-{yymm}-{SECRET_KEY}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        var hexString = Convert.ToHexString(hashBytes);
        return hexString[..12].ToUpperInvariant();
    }

    public static LicenseInfo ValidateLicenseKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return new LicenseInfo
            {
                IsValid = false,
                Plan = PlanCode.Free,
                ErrorMessage = "License key is empty."
            };
        }

        var match = LICENSE_KEY_REGEX.Match(key.Trim());
        if (!match.Success)
        {
            return new LicenseInfo
            {
                IsValid = false,
                Plan = PlanCode.Free,
                ErrorMessage = "Invalid license key format."
            };
        }

        var productCode = match.Groups[1].Value;
        var planStr = match.Groups[2].Value;
        var yymm = match.Groups[3].Value;
        var sigPart1 = match.Groups[4].Value;
        var sigPart2 = match.Groups[5].Value;
        var sigPart3 = match.Groups[6].Value;

        if (productCode != PRODUCT_CODE)
        {
            return new LicenseInfo
            {
                IsValid = false,
                ProductCode = productCode,
                Plan = PlanCode.Free,
                ErrorMessage = $"Invalid product code: {productCode}. Expected: {PRODUCT_CODE}."
            };
        }

        var expectedSignature = GenerateSignature(productCode, planStr, yymm);
        var actualSignature = $"{sigPart1}{sigPart2}{sigPart3}";

        if (!string.Equals(expectedSignature, actualSignature, StringComparison.OrdinalIgnoreCase))
        {
            return new LicenseInfo
            {
                IsValid = false,
                ProductCode = productCode,
                Plan = PlanCode.Free,
                ErrorMessage = "Invalid license key signature."
            };
        }

        var plan = ParsePlanCode(planStr);

        // Parse expiry: yymm represents the issue date; license expires 12 months later
        DateTime? expiresAt = null;
        if (yymm.Length == 4
            && int.TryParse(yymm[..2], out var yy)
            && int.TryParse(yymm[2..], out var mm)
            && mm >= 1 && mm <= 12)
        {
            var issueYear = 2000 + yy;
            var issueDate = new DateTime(issueYear, mm, 1);
            expiresAt = issueDate.AddMonths(12);

            if (DateTime.UtcNow >= expiresAt.Value)
            {
                return new LicenseInfo
                {
                    IsValid = false,
                    ProductCode = productCode,
                    Plan = plan,
                    YearMonth = yymm,
                    ExpiresAt = expiresAt,
                    ErrorMessage = $"License expired on {expiresAt.Value:yyyy-MM-dd}."
                };
            }
        }

        return new LicenseInfo
        {
            IsValid = true,
            ProductCode = productCode,
            Plan = plan,
            YearMonth = yymm,
            ExpiresAt = expiresAt,
        };
    }

    public static bool CanUseFeature(PlanCode plan, string feature)
    {
        if (!FEATURE_MATRIX.TryGetValue(feature, out var allowedPlans))
            return true;

        return allowedPlans.Contains(plan);
    }

    public static string GetPlanDisplayName(PlanCode plan)
    {
        return plan switch
        {
            PlanCode.Free => "\u30D5\u30EA\u30FC",          // フリー
            PlanCode.Std  => "\u30B9\u30BF\u30F3\u30C0\u30FC\u30C9", // スタンダード
            PlanCode.Pro  => "\u30D7\u30ED",                // プロ
            PlanCode.Ent  => "\u30A8\u30F3\u30BF\u30FC\u30D7\u30E9\u30A4\u30BA", // エンタープライズ
            _             => plan.ToString(),
        };
    }

    public static string GenerateLicenseKey(PlanCode plan, string yymm)
    {
        var planStr = plan switch
        {
            PlanCode.Std => "STD",
            PlanCode.Pro => "PRO",
            PlanCode.Ent => "ENT",
            _ => throw new ArgumentException($"Cannot generate license key for plan: {plan}")
        };

        var signature = GenerateSignature(PRODUCT_CODE, planStr, yymm);
        var part1 = signature[..4];
        var part2 = signature[4..8];
        var part3 = signature[8..12];

        return $"{PRODUCT_CODE}-{planStr}-{yymm}-{part1}-{part2}-{part3}";
    }

    private static PlanCode ParsePlanCode(string planStr)
    {
        return planStr.ToUpperInvariant() switch
        {
            "STD" => PlanCode.Std,
            "PRO" => PlanCode.Pro,
            "ENT" => PlanCode.Ent,
            _     => PlanCode.Free,
        };
    }
}
