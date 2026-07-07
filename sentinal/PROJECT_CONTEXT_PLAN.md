# PROJECT_CONTEXT.md

# SentinelX -- Master Project Context

## Vision

Build **SentinelX**, a Windows-first AI-powered cybersecurity
investigation platform.

The Windows agent should: - Collect comprehensive Windows telemetry. -
Normalize and correlate events. - Build a chronological incident
timeline. - Detect system changes. - Produce structured JSON for a
reasoning AI. - Support future dashboard, API, and plugin architecture.

## Current Architecture

1.  Telemetry Collection
2.  Event Normalization
3.  Correlation Engine
4.  Timeline Builder
5.  Change Detection
6.  Report Generator
7.  AI Reasoning
8.  Dashboard

## Guiding Principles

-   Modular architecture.
-   Preserve raw evidence.
-   AI reasons on structured data, not raw logs.
-   JSON is the interface between the agent and AI.
-   Extensible collector design.

## Hardware Constraints

-   Windows laptop
-   Intel Core i7-1255U
-   16 GB RAM
-   Development should support local LLM workflows when practical.

## Coding Standards

-   Prefer C#/.NET for Windows agent.
-   Clean architecture.
-   Strong typing.
-   Extensive logging.
-   Unit-testable modules.

## AI Assistant Instructions

When working on this repository: - Never rewrite working code
unnecessarily. - Preserve modularity. - Explain design decisions. -
Suggest improvements before major refactors. - Keep security,
performance, and maintainability in mind.

## Roadmap

### Phase 1

-   Telemetry collectors
-   Timeline
-   JSON export

### Phase 2

-   AI integration
-   Dashboard
-   Risk scoring

### Phase 3

-   Plugin system
-   Fleet management
-   Historical comparison
-   Threat intelligence enrichment

## Session Goal

Treat this file as the authoritative project context for future Gemini
Antigravity sessions.
