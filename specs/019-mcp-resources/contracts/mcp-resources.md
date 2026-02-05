# MCP Resource Contracts

**Feature**: 019-mcp-resources | **Date**: 2026-02-05

These contracts define the MCP protocol interactions for debugger resources.

## Server Capabilities Declaration

```json
{
  "capabilities": {
    "resources": {
      "subscribe": true,
      "listChanged": true
    }
  }
}
```

## resources/list

### Request
```json
{
  "method": "resources/list",
  "params": {}
}
```

### Response (session active)
```json
{
  "resources": [
    {
      "uri": "debugger://session",
      "name": "Debug Session",
      "description": "Current debug session state (process info, state, location)",
      "mimeType": "application/json"
    },
    {
      "uri": "debugger://breakpoints",
      "name": "Breakpoints",
      "description": "All active breakpoints, tracepoints, and exception breakpoints",
      "mimeType": "application/json"
    },
    {
      "uri": "debugger://threads",
      "name": "Threads",
      "description": "Managed threads in the debugged process",
      "mimeType": "application/json"
    }
  ]
}
```

### Response (no session)
```json
{
  "resources": []
}
```

## resources/templates/list

### Request
```json
{
  "method": "resources/templates/list",
  "params": {}
}
```

### Response (session active)
```json
{
  "resourceTemplates": [
    {
      "uriTemplate": "debugger://source/{+file}",
      "name": "Source File",
      "description": "Source code from PDB-referenced files in the debugged process",
      "mimeType": "text/plain"
    }
  ]
}
```

### Response (no session)
```json
{
  "resourceTemplates": []
}
```

## resources/read — Session

### Request
```json
{
  "method": "resources/read",
  "params": {
    "uri": "debugger://session"
  }
}
```

### Response
```json
{
  "contents": [
    {
      "uri": "debugger://session",
      "mimeType": "application/json",
      "text": "{\"processId\":1234,\"processName\":\"MyApp\",\"executablePath\":\"/path/to/MyApp.dll\",\"runtimeVersion\":\".NET 10.0.0\",\"state\":\"Paused\",\"launchMode\":\"Launch\",\"attachedAt\":\"2026-02-05T10:30:00+00:00\",\"pauseReason\":\"Breakpoint\",\"currentLocation\":{\"file\":\"/src/Program.cs\",\"line\":42,\"column\":1,\"functionName\":\"Main\",\"moduleName\":\"MyApp.dll\"},\"activeThreadId\":1,\"commandLineArgs\":[\"--verbose\"],\"workingDirectory\":\"/app\"}"
    }
  ]
}
```

## resources/read — Breakpoints

### Request
```json
{
  "method": "resources/read",
  "params": {
    "uri": "debugger://breakpoints"
  }
}
```

### Response
```json
{
  "contents": [
    {
      "uri": "debugger://breakpoints",
      "mimeType": "application/json",
      "text": "{\"breakpoints\":[{\"id\":\"bp-abc123\",\"type\":\"Breakpoint\",\"file\":\"/src/Program.cs\",\"line\":42,\"column\":null,\"enabled\":true,\"verified\":true,\"state\":\"Bound\",\"hitCount\":3,\"condition\":null,\"logMessage\":null,\"hitCountMultiple\":0,\"maxNotifications\":0,\"notificationsSent\":0}],\"exceptionBreakpoints\":[{\"id\":\"ex-def456\",\"exceptionType\":\"System.NullReferenceException\",\"breakOnFirstChance\":true,\"breakOnSecondChance\":true,\"includeSubtypes\":true,\"enabled\":true,\"verified\":true,\"hitCount\":0}]}"
    }
  ]
}
```

## resources/read — Threads

### Request
```json
{
  "method": "resources/read",
  "params": {
    "uri": "debugger://threads"
  }
}
```

### Response (paused — fresh)
```json
{
  "contents": [
    {
      "uri": "debugger://threads",
      "mimeType": "application/json",
      "text": "{\"threads\":[{\"id\":1,\"name\":\"Main Thread\",\"state\":\"Suspended\",\"isCurrent\":true,\"location\":{\"file\":\"/src/Program.cs\",\"line\":42,\"column\":1,\"functionName\":\"Main\",\"moduleName\":\"MyApp.dll\"}},{\"id\":4,\"name\":\"Thread Pool Worker\",\"state\":\"Suspended\",\"isCurrent\":false,\"location\":null}],\"stale\":false,\"capturedAt\":\"2026-02-05T10:30:05+00:00\"}"
    }
  ]
}
```

### Response (running — stale)
```json
{
  "contents": [
    {
      "uri": "debugger://threads",
      "mimeType": "application/json",
      "text": "{\"threads\":[...],\"stale\":true,\"capturedAt\":\"2026-02-05T10:30:05+00:00\"}"
    }
  ]
}
```

## resources/read — Source File

### Request
```json
{
  "method": "resources/read",
  "params": {
    "uri": "debugger://source//src/Program.cs"
  }
}
```

### Response (file found and allowed)
```json
{
  "contents": [
    {
      "uri": "debugger://source//src/Program.cs",
      "mimeType": "text/plain",
      "text": "using System;\n\nnamespace MyApp;\n\nclass Program\n{\n    static void Main(string[] args)\n    {\n        Console.WriteLine(\"Hello\");\n    }\n}\n"
    }
  ]
}
```

### Error — path not in PDB symbols
```json
{
  "error": {
    "code": -1,
    "message": "File path '/etc/passwd' is not referenced in any loaded PDB symbols"
  }
}
```

### Error — file not on disk
```json
{
  "error": {
    "code": -1,
    "message": "Source file '/src/Deleted.cs' referenced in PDB but not found on disk"
  }
}
```

## resources/subscribe

### Request
```json
{
  "method": "resources/subscribe",
  "params": {
    "uri": "debugger://session"
  }
}
```

### Response
```json
{
  "result": {}
}
```

## resources/unsubscribe

### Request
```json
{
  "method": "resources/unsubscribe",
  "params": {
    "uri": "debugger://session"
  }
}
```

### Response
```json
{
  "result": {}
}
```

## Notifications (Server → Client)

### Resource list changed (session started/ended)
```json
{
  "method": "notifications/resources/list_changed"
}
```

### Resource updated (subscribed resource content changed)
```json
{
  "method": "notifications/resources/updated",
  "params": {
    "uri": "debugger://session"
  }
}
```
