using Checklist.Domain;

namespace Api.Tests.Checklist.DomainTests;

public sealed class ItemGroupSettingsInputTests
{
    [Theory]
    [InlineData(1439, null)]
    [InlineData(525601, null)]
    [InlineData(0, null)]
    [InlineData(null, -1)]
    [InlineData(null, 525601)]
    public void TryCreate_RejectsValuesOutOfRange(
        int? autoDeleteAfterMinutes,
        int? hideCompletedAfterMinutes
    )
    {
        Assert.False(
            ItemGroupSettingsInput.TryCreate(
                autoDeleteAfterMinutes,
                hideCompletedAfterMinutes,
                protectReservations: false,
                out ItemGroupSettingsInput? settings
            )
        );
        Assert.Null(settings);
    }

    [Theory]
    [InlineData(1440, 0, false)]
    [InlineData(525600, 525600, true)]
    [InlineData(null, null, false)]
    public void TryCreate_AcceptsTheLimits_AndOff(
        int? autoDeleteAfterMinutes,
        int? hideCompletedAfterMinutes,
        bool protectReservations
    )
    {
        Assert.True(
            ItemGroupSettingsInput.TryCreate(
                autoDeleteAfterMinutes,
                hideCompletedAfterMinutes,
                protectReservations,
                out ItemGroupSettingsInput? settings
            )
        );
        Assert.Equal(autoDeleteAfterMinutes, settings.AutoDeleteAfterMinutes);
        Assert.Equal(hideCompletedAfterMinutes, settings.HideCompletedAfterMinutes);
        Assert.Equal(protectReservations, settings.ProtectReservations);
    }
}
