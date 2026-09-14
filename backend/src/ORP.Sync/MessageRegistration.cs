using System;
using System.Collections.Generic;
using System.Linq;

namespace ORP.Sync;

internal static class MessageRegistration
{
    public static RegistrationWorkflow Resolve(
        RegistrationCandidate candidate,
        IEnumerable<RegistrationWorkflow> workflows) =>
        workflows
            .Where(workflow => workflow.IsValid
                && string.Equals(workflow.MessageType, candidate.MessageType, StringComparison.OrdinalIgnoreCase)
                && workflow.DepartmentId == candidate.DepartmentId
                && (workflow.BranchId == candidate.BranchId || workflow.BranchId == null))
            .OrderBy(workflow => workflow.BranchId == candidate.BranchId ? 0 : 1)
            .ThenBy(workflow => workflow.Id)
            .FirstOrDefault();
}

internal sealed class RegistrationCandidate
{
    public RegistrationCandidate(long messageId, string messageType, int branchId, int departmentId)
    {
        MessageId = messageId;
        MessageType = messageType;
        BranchId = branchId;
        DepartmentId = departmentId;
    }

    public long MessageId { get; }
    public string MessageType { get; }
    public int BranchId { get; }
    public int DepartmentId { get; }
}

internal sealed class RegistrationWorkflow
{
    public RegistrationWorkflow(int id, string messageType, int departmentId, int? branchId)
    {
        Id = id;
        MessageType = messageType;
        DepartmentId = departmentId;
        BranchId = branchId;
    }

    public int Id { get; }
    public string MessageType { get; }
    public int DepartmentId { get; }
    public int? BranchId { get; }
    public List<RegistrationStep> Steps { get; } = new List<RegistrationStep>();

    public bool IsValid
    {
        get
        {
            if (!Steps.Any(step => step.Required && step.ReviewLevel == 1)) return false;
            if (Steps.Any(step => step.ReviewLevel < 1 || step.ReviewLevel > 3)) return false;
            if (Steps.GroupBy(step => step.ReviewLevel).Any(group => group.Count() > 1)) return false;

            var requiredSteps = Steps.Where(step => step.Required).OrderBy(step => step.Order).ToArray();
            for (var index = 1; index < requiredSteps.Length; index++)
            {
                if (requiredSteps[index].ReviewLevel <= requiredSteps[index - 1].ReviewLevel) return false;
            }

            return true;
        }
    }
}

internal sealed class RegistrationStep
{
    public RegistrationStep(int order, int reviewLevel, bool required)
    {
        Order = order;
        ReviewLevel = reviewLevel;
        Required = required;
    }

    public int Order { get; }
    public int ReviewLevel { get; }
    public bool Required { get; }
}
