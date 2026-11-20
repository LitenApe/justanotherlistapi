using Checklist.Domain;

namespace Api.Tests.Checklist.DomainTests;

public sealed class ReservationChangeTests
{
    private static readonly Guid alice = Guid.NewGuid();
    private static readonly Guid bob = Guid.NewGuid();
    private static readonly Guid carol = Guid.NewGuid();

    [Fact]
    public void Decide_LetsAnyMemberReserveAFreeItem_ForThemselvesOrSomeoneElse()
    {
        foreach (bool protect in new[] { false, true })
        {
            Assert.Equal(
                ReservationDecision.Change,
                Reserve(alice, alice, replacing: null).Decide(null, null, protect, true)
            );
            Assert.Equal(
                ReservationDecision.Change,
                Reserve(alice, bob, replacing: null).Decide(null, null, protect, true)
            );
        }
    }

    [Fact]
    public void Decide_ChangesNothing_WhenTheItemIsAlreadyReservedThatWay()
    {
        // Even with a stale view, in a protected list: repeating a request is harmless.
        Assert.Equal(
            ReservationDecision.Unchanged,
            Reserve(alice, bob, replacing: null).Decide(bob, alice, true, true)
        );
        Assert.Equal(
            ReservationDecision.Unchanged,
            Cancel(alice, seen: bob).Decide(null, null, true, true)
        );
    }

    [Fact]
    public void Decide_RefusesToReserveForSomeoneOutsideTheList()
    {
        Assert.Equal(
            ReservationDecision.ForNonMember,
            Reserve(alice, bob, replacing: null).Decide(null, null, false, memberBelongs: false)
        );
    }

    [Fact]
    public void Decide_RefusesAChange_WhenTheReservationIsNotTheOneTheCallerSaw()
    {
        // Saw it free, but Bob reserved it first: two people never get the same item.
        Assert.Equal(
            ReservationDecision.Outdated,
            Reserve(alice, alice, replacing: null).Decide(bob, bob, false, true)
        );
        // Saw it reserved for Bob, but Carol has taken it over since.
        Assert.Equal(
            ReservationDecision.Outdated,
            Reserve(alice, alice, replacing: bob).Decide(carol, carol, false, true)
        );
        Assert.Equal(
            ReservationDecision.Outdated,
            Cancel(alice, seen: bob).Decide(carol, carol, false, true)
        );
        // Saw it reserved for Bob, but it's free now.
        Assert.Equal(
            ReservationDecision.Outdated,
            Reserve(alice, carol, replacing: bob).Decide(null, null, false, true)
        );
    }

    [Fact]
    public void Decide_LetsAnyMemberTakeOverOrCancelAReservation_WhenTheListDoesNotProtectThem()
    {
        Assert.Equal(
            ReservationDecision.Change,
            Reserve(carol, carol, replacing: bob).Decide(bob, alice, false, true)
        );
        Assert.Equal(
            ReservationDecision.Change,
            Reserve(carol, alice, replacing: bob).Decide(bob, alice, false, true)
        );
        Assert.Equal(
            ReservationDecision.Change,
            Cancel(carol, seen: bob).Decide(bob, alice, false, true)
        );
    }

    [Fact]
    public void Decide_LetsOnlyTheReserveeAndTheReserverChangeAReservation_WhenTheListProtectsThem()
    {
        // Alice reserved the item for Bob. Carol has nothing to do with it.
        Assert.Equal(
            ReservationDecision.Protected,
            Reserve(carol, carol, replacing: bob).Decide(bob, alice, true, true)
        );
        Assert.Equal(
            ReservationDecision.Protected,
            Cancel(carol, seen: bob).Decide(bob, alice, true, true)
        );

        // Bob, who it's for, can hand it on or cancel it; Alice, who reserved it, can too.
        Assert.Equal(
            ReservationDecision.Change,
            Reserve(bob, carol, replacing: bob).Decide(bob, alice, true, true)
        );
        Assert.Equal(
            ReservationDecision.Change,
            Cancel(bob, seen: bob).Decide(bob, alice, true, true)
        );
        Assert.Equal(
            ReservationDecision.Change,
            Reserve(alice, carol, replacing: bob).Decide(bob, alice, true, true)
        );
        Assert.Equal(
            ReservationDecision.Change,
            Cancel(alice, seen: bob).Decide(bob, alice, true, true)
        );
    }

    private static ReservationChange Reserve(Guid caller, Guid member, Guid? replacing) =>
        new(caller, member, replacing);

    private static ReservationChange Cancel(Guid caller, Guid seen) => new(caller, null, seen);
}
