namespace EdgeParse.Core.Pipeline.Stages;

using EdgeParse.Core.Models;

/// <summary>
/// Stage 17: Assigns hierarchical nesting levels based on heading levels and indentation.
/// Each heading creates a new nesting context; content under it is nested at that level.
/// </summary>
public static class NestingAssigner
{
    public static void AssignNestingLevels(List<ContentElement> elements)
    {
        if (elements.Count == 0) return;

        // Track the current heading level stack
        int currentNestingLevel = 0;
        var headingStack = new Stack<int>(); // heading levels

        foreach (var elem in elements)
        {
            if (elem.Type == ContentElementType.Heading)
            {
                int headingLevel = elem.Heading!.HeadingLevel ?? 1;

                // Pop headings of same or deeper level
                while (headingStack.Count > 0 && headingStack.Peek() >= headingLevel)
                {
                    headingStack.Pop();
                }

                headingStack.Push(headingLevel);
                currentNestingLevel = headingStack.Count - 1;

                // Set the heading's nesting level
                elem.Heading.Base.Base.Level = currentNestingLevel.ToString();
            }
            else if (elem.Type == ContentElementType.NumberHeading)
            {
                int headingLevel = elem.NumberHeading!.Base.HeadingLevel ?? 1;

                while (headingStack.Count > 0 && headingStack.Peek() >= headingLevel)
                {
                    headingStack.Pop();
                }

                headingStack.Push(headingLevel);
                currentNestingLevel = headingStack.Count - 1;

                elem.NumberHeading.Base.Base.Base.Level = currentNestingLevel.ToString();
            }
            else if (elem.Type == ContentElementType.Paragraph)
            {
                elem.Paragraph!.Base.Level = currentNestingLevel.ToString();
            }
            else if (elem.Type == ContentElementType.List)
            {
                // Lists under a heading inherit its nesting level
            }
        }
    }
}
