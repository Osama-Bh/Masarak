# Company Employment Records API Documentation

## Overview

This page uses two separate endpoints:

- one endpoint for the historical applications table
- one endpoint for the filter dropdowns of the `سجلات الطلبات` page

These applications are historical company-side records and are no longer considered active applications in the company applications page.

## 1. Employment Records Endpoint

**Method:** `GET`  
**URL:** `/api/Applications/company/employment-records`

## Authorization

This endpoint is intended for authenticated company users.

## Query Parameters

All parameters are optional unless stated otherwise.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `Page` | int | No | Page number. Default is `1`. |
| `PageSize` | int | No | Number of items per page. Default is `10`. |
| `Search` | string | No | Filters by candidate full name or job title. |
| `ApplicationStatusId` | int | No | Filters by one of the supported historical statuses only. |
| `JobId` | int | No | Filters by job. |

## Supported Status Filter Values For Employment Records

The `ApplicationStatusId` filter for this page is intended to work only with these historical statuses:

- `Withdrawn`
- `MissingInterview`
- `Hired`
- `Rejected`

Frontend should load these values from the dedicated employment-records filters endpoint instead of the general company filters endpoint.

## Business Rules

An application is returned by this endpoint only if it belongs to the company and matches one of the following rules:

### 1. Withdrawn

Return the application when:

- `ApplicationStatus = Withdrawn`
- `ApplicationDate` is before today

Example:

- Application date: `2026-08-20`
- Current date: `2026-08-21`
- Result: the application appears in `سجلات الطلبات`

### 2. MissingInterview

Return the application when:

- `ApplicationStatus = MissingInterview`
- the latest interview date is before today

### 3. Rejected

Return the application when:

- `ApplicationStatus = Rejected`
- and one of the following is true:
- the application has at least one interview and the latest interview date is before today
- the application has no interview and `ApplicationDate` is before today

Important:

- cancelled interviews are represented through the application becoming `Rejected`
- there is no separate company application status called `Cancelled`

### 4. Hired

Return the application when:

- `ApplicationStatus = Hired`
- and one of the following is true:
- the application has at least one interview and the latest interview date is before today
- the application has no interview and `ApplicationDate` is before today

## Active Applications Behavior

Applications returned by this endpoint are removed from the company active applications endpoint:

- `GET /api/Applications/company`

That active endpoint now returns only active company applications.

## Interview Page Behavior

If an application is no longer active and becomes part of employment records, its interview should no longer appear in the company interviews page.

Affected interview page endpoint:

- `GET /api/Interviews/company`

## Employment Records Response Shape

The endpoint returns a paginated result.

```json
{
  "statusCode": 200,
  "data": {
    "items": [
      {
        "applicationId": 123,
        "profilePhoto": "https://...",
        "fullName": "Candidate Name",
        "email": "candidate@example.com",
        "jobTitle": "Backend Developer",
        "applicationDate": "2026-08-20T00:00:00Z",
        "matchingPercentage": 87,
        "applicationStatus": "Rejected",
        "cvDownloadUrl": "https://...",
        "canReject": false,
        "canSchedule": false,
        "canHire": false
      }
    ],
    "currentPage": 1,
    "pageSize": 10,
    "totalCount": 1
  },
  "message": null
}
```

## Employment Records Response Fields

| Field | Type | Description |
|---|---|---|
| `applicationId` | int | Application ID. |
| `profilePhoto` | string \| null | Candidate profile photo URL. |
| `fullName` | string | Candidate full name. |
| `email` | string | Candidate email. |
| `jobTitle` | string | Job title. |
| `applicationDate` | datetime | Original application date. |
| `matchingPercentage` | int \| null | Matching percentage for the application. |
| `applicationStatus` | string | Current application status. |
| `cvDownloadUrl` | string \| null | Candidate CV download URL. |
| `canReject` | bool | Action flag from backend. Usually `false` for historical records. |
| `canSchedule` | bool | Action flag from backend. Usually `false` for historical records. |
| `canHire` | bool | Action flag from backend. Usually `false` for historical records. |

## 2. Employment Records Filters Endpoint

**Method:** `GET`  
**URL:** `/api/Applications/company/employment-records/filters`

## Filters Endpoint Purpose

This endpoint returns the dropdown data for the `سجلات الطلبات` page filters.

Use it to populate:

- the status filter
- the jobs filter

## Filters Endpoint Response Shape

```json
{
  "statusCode": 200,
  "data": {
    "statuses": [
      {
        "id": 5,
        "name": "Withdrawn"
      },
      {
        "id": 6,
        "name": "MissingInterview"
      },
      {
        "id": 7,
        "name": "Rejected"
      },
      {
        "id": 8,
        "name": "Hired"
      }
    ],
    "jobs": [
      {
        "id": 12,
        "name": "Backend Developer"
      },
      {
        "id": 18,
        "name": "UI Designer"
      }
    ]
  },
  "message": null
}
```

## Filters Endpoint Response Fields

| Field | Type | Description |
|---|---|---|
| `statuses` | array | Historical statuses allowed for this page filter only. |
| `jobs` | array | Jobs belonging to the authenticated company. |
| `statuses[].id` | int | Status ID to send back as `ApplicationStatusId`. |
| `statuses[].name` | string | Display name for the status dropdown. |
| `jobs[].id` | int | Job ID to send back as `JobId`. |
| `jobs[].name` | string | Display name for the job dropdown. |

## Frontend Integration Notes

- Use `GET /api/Applications/company/employment-records` specifically for the `سجلات الطلبات` table.
- Do not use `GET /api/Applications/company` for this page.
- Do not use `GET /api/Applications/company/filters` for this page filter dropdown.
- Use `GET /api/Applications/company/employment-records/filters` to populate the status and job filters for this page.
- Pagination is server-side.
- Search is server-side.
- Status filtering is server-side.
- Job filtering is server-side.
- `applicationStatus` is returned as a display string.
- `applicationDate` is the application date, not the interview date.

## Suggested Frontend Query Example

```http
GET /api/Applications/company/employment-records?Page=1&PageSize=10&Search=Ahmed&ApplicationStatusId=7&JobId=5
```

## Suggested Filters Request Example

```http
GET /api/Applications/company/employment-records/filters
```

## Notes For Another Model

If another model is integrating this page, it should assume:

- this page is for historical company records only
- the active applications page and the employment records page must use different endpoints
- the active applications filters endpoint and the employment records filters endpoint must also be different
- rejected applications may represent previously cancelled interviews
- once an application is historical, its interview should not continue to appear on the company interview page
- the filter status options for this page are limited to `Withdrawn`, `MissingInterview`, `Hired`, and `Rejected`
