using Datapoint.Terminal.Agent.Safety;

namespace Datapoint.Terminal.Agent.Tests.Safety;

public sealed class StepHazardDetectorTests
{
    [Theory]
    [InlineData("sudo reboot", StepHazards.EndsConnection)]
    [InlineData("apt-get -y upgrade && shutdown -r now", StepHazards.EndsConnection)]
    [InlineData("sudo systemctl restart sshd", StepHazards.EndsConnection)]
    [InlineData("sudo mkfs.ext4 /dev/sdb1", StepHazards.ErasesDisk)]
    [InlineData("dd if=image.iso of=/dev/sdb bs=4M", StepHazards.ErasesDisk)]
    [InlineData("sudo rm -rf /var/www/old", StepHazards.DeletesRecursively)]
    [InlineData("ssh-keygen -R web1", StepHazards.ChangesTrust)]
    [InlineData("docker node ls", StepHazards.None)]
    [InlineData("systemctl restart nginx", StepHazards.None)]
    [InlineData("rm old.log", StepHazards.None)]
    [InlineData("grep reboot /var/log/syslog", StepHazards.None)]
    [InlineData("echo rm -rf / > notes.txt", StepHazards.None)]
    [InlineData("cd /tmp; rm -r build", StepHazards.DeletesRecursively)]
    public void Flags_steps_that_are_risky_or_hard_to_undo(string command, StepHazards expected) =>
        Assert.Equal(expected, StepHazardDetector.Detect(command));
}
