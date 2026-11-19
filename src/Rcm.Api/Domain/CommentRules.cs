namespace Rcm.Api.Domain;

// Anyone who can see an item may comment while its month is open. Visibility is checked by the caller.
public static class CommentRules
{
    public static Result<string> Check(bool monthOpen, string? text)
    {
        if (!monthOpen)
        {
            return Error.Invalid("The month is closed. Its items are read-only.");
        }

        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return Error.Invalid("A comment needs text.");
        }

        if (trimmed.Length > WorkflowRules.MaxTextLength)
        {
            return Error.Invalid($"A comment can be at most {WorkflowRules.MaxTextLength} characters.");
        }

        return trimmed;
    }
}
