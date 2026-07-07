# SentinelX - Detailed Architecture & Design Notes

## Vision

SentinelX is a Windows security agent whose purpose is to collect system
telemetry, transform it into a structured incident timeline, and pass
that structured report to a separate AI reasoning engine.

The philosophy is:

> Do not let the AI waste time parsing raw logs. Let the Windows agent
> do all preprocessing. Let the AI focus only on reasoning.

------------------------------------------------------------------------

# High-Level Pipeline

``` text
Windows

↓

SentinelX Agent

    1. Telemetry Collection
    2. Event Normalization
    3. Correlation Engine
    4. Timeline Builder
    5. Change Detection
    6. Incident Report Generation

↓

Structured JSON Report

↓

Reasoning AI

↓

Executive Summary + Technical Analysis

↓

Web API

↓

Dashboard
```

------------------------------------------------------------------------

# SentinelX Agent

The Windows agent performs every preprocessing task before the AI.

## Module 1 -- Telemetry Collection

Purpose: Collect operating system activity from configured Windows
telemetry sources.

Design goals: - Timestamp every event. - Preserve original event data. -
Tag each event with its source. - Avoid altering raw evidence.

Output:

``` json
{
 "timestamp":"...",
 "source":"...",
 "event_type":"...",
 "raw_data":{}
}
```

------------------------------------------------------------------------

## Module 2 -- Event Normalization

Different telemetry sources produce different formats.

This module converts everything into one internal schema.

Example:

``` json
{
 "timestamp":"...",
 "category":"Process",
 "action":"Created",
 "user":"...",
 "details":{}
}
```

Benefits: - Every later module works on the same structure. - Easier
searching. - Easier exporting. - Easier AI prompting.

------------------------------------------------------------------------

## Module 3 -- Correlation Engine

Purpose: Find relationships.

Examples:

Process A

↓

creates File B

↓

modifies Registry C

↓

opens Network Connection D

↓

creates Scheduled Task E

Instead of five unrelated events, they become one activity chain.

Outputs: - Parent-child relationships - Session grouping - Process
trees - Related changes

------------------------------------------------------------------------

## Module 4 -- Timeline Builder

Purpose: Create a chronological story.

Example:

09:10 Login

↓

09:11 Explorer

↓

09:12 PowerShell

↓

09:13 Registry Modified

↓

09:14 Network Connection

↓

09:15 Service Created

The timeline becomes the backbone of the investigation.

------------------------------------------------------------------------

## Module 5 -- Change Detection

Determine what changed during the observation period.

Possible sections: - Processes - Files - Registry - Services - Scheduled
Tasks - Users - Network - Startup Items

Each section should describe: - Created - Modified - Deleted (if
known) - Time - Related process/user (when available)

------------------------------------------------------------------------

## Module 6 -- Report Generator

Produces one structured incident package.

Suggested layout:

``` json
{
 "system":{},
 "timeline":[],
 "users":[],
 "processes":[],
 "files":[],
 "registry":[],
 "services":[],
 "network":[],
 "changes":[],
 "relationships":[],
 "statistics":{}
}
```

This report is the ONLY input to the reasoning AI.

------------------------------------------------------------------------

# Reasoning AI

Responsibilities: - Explain what happened. - Identify likely
objective. - Describe supporting evidence. - Produce confidence score. -
Suggest investigation priorities.

The AI should consume structured evidence, not raw telemetry.

------------------------------------------------------------------------

# Dashboard

Display: - Timeline - Incident summary - Evidence - Related events -
Confidence - Exportable JSON

------------------------------------------------------------------------

# Guiding Principles

1.  Modular design.
2.  One Windows application handles preprocessing.
3.  Separate AI handles reasoning.
4.  JSON is the interface between the agent and AI.
5.  Add new telemetry sources later without redesigning the pipeline.

------------------------------------------------------------------------

# Future Ideas

-   Plug-in collector architecture
-   Real-time streaming mode
-   Historical incident comparison
-   MITRE ATT&CK mapping
-   Risk scoring
-   Multiple AI specialists (reasoning, reporting, remediation)
-   Remote fleet management
-   Threat intelligence enrichment
-   Incident replay

------------------------------------------------------------------------

# Development Order

1.  Telemetry collection
2.  Normalization
3.  Correlation
4.  Timeline
5.  Change detection
6.  Report generation
7.  AI integration
8.  Web API
9.  Dashboard
10. Continuous improvements
