namespace EventStorming.Mcp.Server;

/// <summary>What a client passes to its model when it connects: how to use this server well.</summary>
internal static class ServerInstructions
{
    public const string Text = """
        This server draws EventStorming boards that people can open and edit together, live. Your API key belongs to one team; you see and change only its boards.

        To draw:
        1. Call list_element_types first and use only its type ids. It also says how each type is written.
        2. Describe the whole model and call create_board once: elements in timeline order (the array order is left to right in time), with keys, swimlanes listed first, anchors for things that belong under an event, and arrows by key. Leave positions out: the server lays everything out.
        3. To continue a board, call get_board for the ids, then add_to_board. To fix one sticky, use update_element.

        Write Domain Events in the past tense and business language ("Order Placed"), Commands in the imperative ("Place Order"), Policies as "Whenever X, then Y". Put a Hot Spot on anything unclear. Mark only a few Domain Events pivotal.

        If a tool is refused, nothing changed: the result lists every problem with the field and a fix. Apply them all and call it again.

        Share the board link from the results with the user. The resource eventstorming://guide has the full modelling guide and a worked example; the prompts big_picture and process_modelling are ready-made starts.
        """;
}
