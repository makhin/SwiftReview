namespace ORP.Domain.Messages;

public sealed class MessageDepartment
{
    private MessageDepartment() { }
    public MessageDepartment(long messageId, int departmentId, bool canProcess = true)
    {
        MessageId = messageId;
        DepartmentId = departmentId;
        CanProcess = canProcess;
    }

    public long MessageId { get; private set; }
    public bool CanProcess { get; private set; }
    internal void SetProcessing(bool value) => CanProcess = value;

    public int DepartmentId { get; private set; }
}
