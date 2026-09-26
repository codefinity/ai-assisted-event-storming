using System.ComponentModel;
using ModelContextProtocol.Server;

namespace EventStorming.Mcp.Server;

/// <summary>Ready-made starts for the two most common jobs: exploring a domain, and modelling one process.</summary>
[McpServerPromptType]
public sealed class ModellingPrompts
{
    [McpServerPrompt(Name = "big_picture", Title = "Big Picture EventStorming of a domain")]
    [Description("Explore a whole business domain as a Big Picture board: the story as Domain Events, the turning points, who is involved, what is unclear.")]
    public static string BigPicture(
        [Description("The business or domain to explore, e.g. \"online food ordering\".")] string domain,
        [Description("Optional: what to pay most attention to, e.g. \"refunds and complaints\".")] string? focus = null) => $"""
        Run a Big Picture EventStorming of {domain} and draw it on a new board.
        {(focus is null ? string.Empty : $"Pay most attention to: {focus}.\n")}
        1. Call list_element_types and use only its type ids.
        2. Tell the story from beginning to end as Domain Events: past tense, business language ("Order Placed", not "Place order" or "OrderCreatedEvent"), one fact per sticky, typically 15 to 40 of them. List them in the order they happen.
        3. Mark two to four pivotal events: the turning points where the story changes phase.
        4. Add a swimlane per party or department that acts in parallel, and put each event in its lane. List the lanes first.
        5. Add Actors and External Systems next to the events they cause or take part in, anchored below those events.
        6. Add a Hot Spot for everything unclear, risky or contested, anchored below the event it concerns. Be generous: open questions are the most valuable output.
        7. Where a cluster of events clearly speaks its own language, add a boundary and name it after that language.
        8. Call create_board with level "big-picture", the elements in timeline order with keys, and arrows only where cause and effect is not obvious from the order.
        9. If it is refused, fix every listed problem and call create_board again.
        10. Finish with the board's link, the pivotal events, and the Hot Spots worth discussing first.
        """;

    [McpServerPrompt(Name = "process_modelling", Title = "Process Modelling of one process")]
    [Description("Model one business process step by step with the Process Modelling grammar, on a new board or an existing one.")]
    public static string ProcessModelling(
        [Description("The process to model, e.g. \"a customer cancels an order after payment\".")] string process,
        [Description("Optional: the id of an existing board to add it to.")] string? boardId = null) => $"""
        Model this process with Process Modelling EventStorming: {process}.
        1. Call list_element_types and use only its type ids.
        2. Follow the grammar for every step, in this order, so it lays out left to right: an Actor, looking at a Read Model, issues a Command; a system (or an External System) accepts it and a Domain Event happens; a Policy ("Whenever X, then Y") reacts with the next Command.
        3. Commands in the imperative ("Cancel Order"), Domain Events in the past tense ("Order Cancelled"), Policies as "Whenever …, then …".
        4. Put Actors and Read Models before the Command they lead to; anchor Hot Spots below the step they question.
        5. Connect each Command to the Domain Event it causes, and each Policy to the Command it issues.
        {(boardId is null
            ? "6. Call create_board with level \"process-modelling\"."
            : $"6. Call get_board with boardId {boardId} to see what is there, then add_to_board to continue after it.")}
        7. If a call is refused, fix every listed problem and call it again.
        8. Finish with the board's link and any open questions.
        """;
}
