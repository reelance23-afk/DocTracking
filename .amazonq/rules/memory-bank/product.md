# DocTracking - Product Overview

## Project Purpose
DocTracking is a document tracking and routing system designed for organizational use (e.g., universities or government offices). It enables users to submit documents, route them through offices and units, and track their status in real time.

## Value Proposition
- Replaces manual paper-based document routing with a digital, auditable workflow
- Provides real-time notifications when documents move between offices/units
- Gives administrators full visibility into document status, history, and bottlenecks
- Supports QR code-based document lookup for quick physical-to-digital tracking

## Key Features
- Document submission with file attachment, type, priority, and description
- Multi-step routing: documents flow from one office/unit to the next
- Role-based access: Admin, Office (office head and staff), and regular Users
- Real-time SignalR notifications when documents are received, forwarded, or completed
- Audit log (DocumentLog) capturing every action taken on a document
- Admin dashboard with stats, CSV export, and user activity reports
- QR code generation and scanning for physical document lookup
- Paginated, filterable document lists with search, status, office, date range filters
- Debounced search inputs for performance
- Dark/light theme toggle via ThemeService
- Public document tracking page (no login required) via /Track route

## Target Users
- Regular staff: submit documents and track their own submissions
- Office staff: receive, forward, and act on documents assigned to their office/unit
- Office heads: oversee all documents in their office, receive head-specific notifications
- Administrators: manage users, offices, units, view all documents, export reports

## Use Cases
1. A staff member submits a request document → it routes to the relevant office
2. An office head receives a notification → reviews and forwards to a unit or next office
3. An admin exports a CSV report of all documents filtered by date and status
4. Anyone scans a QR code on a physical document → sees its current status and history
