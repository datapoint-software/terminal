using Datapoint.Terminal.Agent.Projects;

namespace Datapoint.Terminal.Agent.Tests.Projects;

public sealed class ConnectionReviewTests
{
    private static readonly ProjectConnection s_web = new() { Name = "web", SshHost = "web1", Environment = "production" };

    private static ConnectionReview Compare(ProjectConnection current, ApprovalMode defaultMode = ApprovalMode.Auto) =>
        ConnectionReview.Compare([current], [s_web], defaultMode);

    [Fact]
    public void Accepts_an_unchanged_connection() =>
        Assert.Equal(new ConnectionReview([s_web], []).Held, Compare(s_web).Held);

    [Fact]
    public void Holds_a_new_container_as_a_changed_address()
    {
        var review = Compare(s_web with { Container = new ProjectContainer { Name = "api" } });

        var change = Assert.Single(review.Held);
        Assert.Equal((ConnectionReview.AddressSetting, "web1", "web1 docker:api"), (change.Setting, change.Confirmed, change.Proposed));
        Assert.Null(Assert.Single(review.Effective).Container);
    }

    [Fact]
    public void Holds_a_label_that_leaves_production_and_keeps_asking()
    {
        var review = Compare(s_web with { Environment = "development" });

        var change = Assert.Single(review.Held);
        Assert.Equal((ConnectionReview.EnvironmentSetting, "production", "development"), (change.Setting, change.Confirmed, change.Proposed));
        Assert.Equal("production", Assert.Single(review.Effective).Environment);
    }

    [Fact]
    public void Applies_a_change_to_production_on_its_own()
    {
        var staging = s_web with { Environment = "staging" };

        var review = ConnectionReview.Compare([s_web], [staging], ApprovalMode.Auto);

        Assert.Empty(review.Held);
        Assert.Equal(s_web, Assert.Single(review.Effective));
    }

    [Fact]
    public void Holds_a_looser_approval_mode_and_keeps_the_confirmed_one()
    {
        var review = Compare(s_web with { ApprovalMode = ApprovalMode.Auto });

        Assert.Equal(ConnectionReview.ApprovalSetting, Assert.Single(review.Held).Setting);
        // The confirmed record has no mode of its own, so the production label's default, Ask, applies again.
        var effective = Assert.Single(review.Effective);
        Assert.Null(effective.ApprovalMode);
        Assert.True(effective.IsProduction);
    }

    [Fact]
    public void Applies_a_stricter_approval_mode_on_its_own()
    {
        var dev = new ProjectConnection { Name = "dev", Host = "10.0.0.9", ApprovalMode = ApprovalMode.Auto };

        var review = ConnectionReview.Compare([dev with { ApprovalMode = ApprovalMode.AskForRisky }], [dev], ApprovalMode.Auto);

        Assert.Empty(review.Held);
        Assert.Equal(ApprovalMode.AskForRisky, Assert.Single(review.Effective).ApprovalMode);
    }

    [Theory]
    [InlineData("web2", null, null, null)]
    [InlineData(null, "10.0.0.5", null, null)]
    [InlineData(null, "web1", "root", 2222)]
    public void Holds_a_new_address(string? sshHost, string? host, string? user, int? port)
    {
        var review = Compare(s_web with { SshHost = sshHost, Host = host, User = user, Port = port });

        Assert.Equal(ConnectionReview.AddressSetting, Assert.Single(review.Held).Setting);
        Assert.Equal("web1", Assert.Single(review.Effective).SshHost);
    }

    [Fact]
    public void Holds_a_new_connection_and_drops_a_removed_one()
    {
        var added = new ProjectConnection { Name = "db", Host = "10.0.0.7", User = "admin" };

        var review = ConnectionReview.Compare([added], [s_web], ApprovalMode.Ask);

        Assert.Empty(review.Effective);
        Assert.Equal(new ConnectionChange("db", ConnectionReview.NewConnection, null, "admin@10.0.0.7"), Assert.Single(review.Held));
    }
}
