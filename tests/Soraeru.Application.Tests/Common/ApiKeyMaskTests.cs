using Shouldly;
using Soraeru.Application.Common;

namespace Soraeru.Application.Tests.Common;

public sealed class ApiKeyMaskTests
{
    [Fact]
    public void Mask_empty_is_placeholder()
    {
        ApiKeyMask.Mask(null).ShouldBe("(未設定)");
        ApiKeyMask.Mask("").ShouldBe("(未設定)");
    }

    [Fact]
    public void Mask_long_key_keeps_prefix_and_suffix()
    {
        ApiKeyMask.Mask("AIzaSyAbCdEfGhIjKlMnOpQrStUvWxYz012345").ShouldBe("AIza…2345");
    }

    [Fact]
    public void LooksConfigured_rejects_replace_placeholder()
    {
        ApiKeyMask.LooksConfigured("REPLACE_WITH_KEY").ShouldBeFalse();
        ApiKeyMask.LooksConfigured("real-secret-key").ShouldBeTrue();
    }
}

public sealed class LlmCostEstimatorTests
{
    [Fact]
    public void Estimate_scales_with_tokens()
    {
        // 1M tokens → 15 NTD
        LlmCostEstimator.EstimateNtd(500_000, 500_000).ShouldBe(15m);
    }

    [Fact]
    public void Estimate_zero_when_no_tokens()
    {
        LlmCostEstimator.EstimateNtd(null, null).ShouldBe(0m);
    }
}
