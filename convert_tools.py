{
  "SessionId": "your-session-id-here",
  "ContextKey": "SampleActions",
  "Actions": [
    {
      "Name": "vibrate_gamepad",
      "Layer": "gamepad",
      "Description": "When {{ char }} wants to physically interact with {{ user }}.",
      "Effect": {
        "Secret": "{{ char }} made {{ user }}'s gamepad vibrate."
      },
      "Arguments": [
        {
          "Name": "strength",
          "Type": "String",
          "Required": true,
          "Description": "The strength of the vibration. Can be 'low', 'medium', or 'high'."
        }
      ]
    },
    {
      "Name": "notion-notion_retrieve_page",
      "Layer": "notion",
      "Description": "When {{ char }} needs to retrieve a page from Notion for {{ user }}.",
      "Effect": {
        "Secret": "{{ char }} retrieved a Notion page for {{ user }}."
      },
      "Arguments": [
        {
          "Name": "pageId",
          "Type": "String",
          "Required": true,
          "Description": "The unique identifier of the Notion page to retrieve."
        }
      ]
    },
    {
      "Name": "notion-notion_append_block_children",
      "Layer": "notion",
      "Description": "When {{ char }} wants to append block children to a Notion block for {{ user }}.",
      "Effect": {
        "Secret": "{{ char }} appended new content to a Notion block for {{ user }}."
      },
      "Arguments": [
        {
          "Name": "blockId",
          "Type": "String",
          "Required": true,
          "Description": "The unique identifier of the Notion block to which the children will be appended."
        },
        {
          "Name": "children",
          "Type": "String",
          "Required": true,
          "Description": "The content to append as new block children. This should be a JSON array representing the new blocks."
        }
      ]
    }
  ]
}
