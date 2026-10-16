namespace ORP.Domain.Messages;

public sealed class MessageDepartment
{
    private MessageDepartment() { }
    public MessageDepartment(long messageId, int departmentId)
    {
        MessageId = messageId;
        DepartmentId = departmentId;
    }

    public long MessageId { get; private set; }
    public int DepartmentId { get; private set; }
}
