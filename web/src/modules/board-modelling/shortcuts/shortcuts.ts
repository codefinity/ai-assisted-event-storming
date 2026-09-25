/** The keyboard map shown in the help overlay. Element type letters come from the registry, not from here. */
export interface ShortcutGroup {
  title: string;
  items: Array<{ action: string; keys: string[] }>;
}

export function modifierKey(): string {
  return typeof navigator !== 'undefined' && /Mac|iPhone|iPad/.test(navigator.platform) ? '⌘' : 'Ctrl';
}

export function shortcutGroups(mod = modifierKey()): ShortcutGroup[] {
  return [
    {
      title: 'Add',
      items: [
        { action: 'Add an element of a type where the pointer is', keys: ['its letter'] },
        { action: 'Choose what to add at a spot', keys: ['Double-click'] },
        { action: 'Commit and start the next sticky along the timeline', keys: ['Tab'] },
        { action: 'Paste lines of text as Domain Events', keys: [mod, 'V'] },
      ],
    },
    {
      title: 'Edit',
      items: [
        { action: 'Edit the text of the selected element', keys: ['Enter'] },
        { action: 'Commit the text', keys: ['Enter'] },
        { action: 'New line in the text', keys: ['Shift', 'Enter'] },
        { action: 'Cancel editing', keys: ['Esc'] },
        { action: 'Delete', keys: ['Delete'] },
        { action: 'Undo your last change', keys: [mod, 'Z'] },
        { action: 'Redo', keys: [mod, 'Shift', 'Z'] },
        { action: 'Copy, cut, paste', keys: [mod, 'C / X / V'] },
        { action: 'Duplicate', keys: [mod, 'D'] },
        { action: 'Nudge (hold Shift for larger steps)', keys: ['Arrows'] },
      ],
    },
    {
      title: 'Select',
      items: [
        { action: 'Select everything', keys: [mod, 'A'] },
        { action: 'Add to or remove from the selection', keys: ['Shift', 'Click'] },
        { action: 'Select an area', keys: ['Shift', 'Drag'] },
        { action: 'Next or previous element on the timeline', keys: ['Tab / Shift+Tab'] },
        { action: 'Clear the selection', keys: ['Esc'] },
      ],
    },
    {
      title: 'View',
      items: [
        { action: 'Pan', keys: ['Drag', 'or Space+Drag'] },
        { action: 'Zoom around the pointer', keys: ['Wheel'] },
        { action: 'Zoom in and out', keys: ['+', '-'] },
        { action: 'Back to 100%', keys: ['0'] },
        { action: 'Fit everything', keys: ['Shift', '1'] },
        { action: 'Search and filter', keys: [mod, 'F'] },
        { action: 'This list', keys: ['?'] },
      ],
    },
  ];
}
