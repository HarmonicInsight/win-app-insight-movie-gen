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
    Trial,
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
    public string? Email { get; set; }
}

public static class License
{
    public const string PRODUCT_CODE = "INMV";
    private const string SECRET_KEY = "insight-series-license-secret-2026";

    // 標準フォーマット: PPPP-PLAN-YYMM-HASH-SIG1-SIG2 (TRIAL|STD|PRO のみ)
    private static readonly Regex LICENSE_KEY_REGEX = new(
        @"^(INSS|INSP|INPY|FGIN|INMV)-(TRIAL|STD|PRO)-(\d{4})-([A-Z0-9]{4})-([A-Z0-9]{4})-([A-Z0-9]{4})$",
        RegexOptions.Compiled);

    private static readonly Dictionary<string, PlanCode[]> FEATURE_MATRIX = new()
    {
        { "subtitle", new[] { PlanCode.Trial, PlanCode.Pro, PlanCode.Ent } },
        { "subtitle_style", new[] { PlanCode.Trial, PlanCode.Pro, PlanCode.Ent } },
        { "transition", new[] { PlanCode.Trial, PlanCode.Pro, PlanCode.Ent } },
        { "pptx_import", new[] { PlanCode.Trial, PlanCode.Pro, PlanCode.Ent } },
    };

    // ── Base32 エンコード (RFC 4648, Python/TypeScript 標準と同一) ──

    private static string ToBase32(byte[] bytes)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new StringBuilder();
        int bits = 0;
        int value = 0;

        foreach (var b in bytes)
        {
            value = (value << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                result.Append(alphabet[(value >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            result.Append(alphabet[(value << (5 - bits)) & 31]);
        }

        return result.ToString();
    }

    // ── メールハッシュ: SHA256 → Base32 → 先頭4文字 ──

    private static string ComputeEmailHash(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return ToBase32(hashBytes)[..4].ToUpperInvariant();
    }

    // ── 署名: HMAC-SHA256 → Base32 → 先頭8文字 ──

    private static string GenerateSignature(string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SECRET_KEY));
        var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return ToBase32(sig)[..8].ToUpperInvariant();
    }

    private static bool VerifySignature(string data, string signature)
    {
        try
        {
            var expected = GenerateSignature(data);
            return string.Equals(expected, signature, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    // ── ライセンスキー検証 ──

    public static LicenseInfo ValidateLicenseKey(string? key, string? email = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return new LicenseInfo
            {
                IsValid = false,
                Plan = PlanCode.Free,
                ErrorMessage = "ライセンスキーが入力されていません。"
            };
        }

        key = key.Trim().ToUpperInvariant();
        var match = LICENSE_KEY_REGEX.Match(key);
        if (!match.Success)
        {
            return new LicenseInfo
            {
                IsValid = false,
                Plan = PlanCode.Free,
                ErrorMessage = "ライセンスキーの形式が正しくありません。"
            };
        }

        var productCode = match.Groups[1].Value;
        var planStr = match.Groups[2].Value;
        var yymm = match.Groups[3].Value;
        var emailHash = match.Groups[4].Value;
        var sig1 = match.Groups[5].Value;
        var sig2 = match.Groups[6].Value;

        // 製品コードチェック
        if (productCode != PRODUCT_CODE)
        {
            return new LicenseInfo
            {
                IsValid = false,
                ProductCode = productCode,
                Plan = PlanCode.Free,
                ErrorMessage = $"このキーは {productCode} 用です。{PRODUCT_CODE} 用のキーを入力してください。"
            };
        }

        // 署名検証: HMAC-SHA256(secret, "{product}-{plan}-{yymm}-{emailHash}")
        var signature = sig1 + sig2;
        var signData = $"{productCode}-{planStr}-{yymm}-{emailHash}";
        if (!VerifySignature(signData, signature))
        {
            return new LicenseInfo
            {
                IsValid = false,
                ProductCode = productCode,
                Plan = PlanCode.Free,
                ErrorMessage = "ライセンスキーが無効です。"
            };
        }

        // メールハッシュ照合
        if (!string.IsNullOrEmpty(email))
        {
            var computedHash = ComputeEmailHash(email);
            if (!string.Equals(emailHash, computedHash, StringComparison.OrdinalIgnoreCase))
            {
                return new LicenseInfo
                {
                    IsValid = false,
                    ProductCode = productCode,
                    Plan = PlanCode.Free,
                    ErrorMessage = "メールアドレスが一致しません。"
                };
            }
        }

        var plan = ParsePlanCode(planStr);

        // 有効期限チェック: YYMM は有効期限月（月末まで有効）
        DateTime? expiresAt = null;
        if (yymm.Length == 4
            && int.TryParse(yymm[..2], out var yy)
            && int.TryParse(yymm[2..], out var mm)
            && mm >= 1 && mm <= 12)
        {
            var year = 2000 + yy;
            expiresAt = mm == 12
                ? new DateTime(year + 1, 1, 1).AddDays(-1)
                : new DateTime(year, mm + 1, 1).AddDays(-1);
            // 月末 23:59:59 まで有効
            expiresAt = expiresAt.Value.Date.AddHours(23).AddMinutes(59).AddSeconds(59);

            if (DateTime.Now > expiresAt.Value)
            {
                return new LicenseInfo
                {
                    IsValid = false,
                    ProductCode = productCode,
                    Plan = plan,
                    YearMonth = yymm,
                    ExpiresAt = expiresAt,
                    Email = email,
                    ErrorMessage = $"ライセンスの有効期限が切れています（{expiresAt.Value:yyyy年MM月dd日}）。"
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
            Email = email,
        };
    }

    // ── ライセンスキー生成（開発・テスト用） ──

    public static string GenerateLicenseKey(PlanCode plan, string email, string yymm)
    {
        var planStr = plan switch
        {
            PlanCode.Trial => "TRIAL",
            PlanCode.Std   => "STD",
            PlanCode.Pro   => "PRO",
            _ => throw new ArgumentException($"Cannot generate license key for plan: {plan}")
        };

        var emailHash = ComputeEmailHash(email);
        var signData = $"{PRODUCT_CODE}-{planStr}-{yymm}-{emailHash}";
        var signature = GenerateSignature(signData);
        var sig1 = signature[..4];
        var sig2 = signature[4..8];

        return $"{PRODUCT_CODE}-{planStr}-{yymm}-{emailHash}-{sig1}-{sig2}";
    }

    // ── 機能チェック ──

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
            PlanCode.Free  => "フリー",
            PlanCode.Trial => "トライアル",
            PlanCode.Std   => "スタンダード",
            PlanCode.Pro   => "プロ",
            PlanCode.Ent   => "エンタープライズ",
            _              => plan.ToString(),
        };
    }

    private static PlanCode ParsePlanCode(string planStr)
    {
        return planStr.ToUpperInvariant() switch
        {
            "TRIAL" => PlanCode.Trial,
            "STD"   => PlanCode.Std,
            "PRO"   => PlanCode.Pro,
            "ENT"   => PlanCode.Ent,
            _       => PlanCode.Free,
        };
    }
}
