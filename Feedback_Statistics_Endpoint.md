# Feedback Statistics API Documentation

## Overview

This endpoint returns the statistics needed to render the four cards at the top of the dashboard feedbacks page.

It is intended for the `إدارة التعليقات` page and follows the same backend response pattern used in the project.

## Endpoint

**Method:** `GET`  
**URL:** `/api/Feedbacks/statistics`

## Authorization

This endpoint is intended for authenticated dashboard users with one of these roles:

- `Admin`
- `SubAdmin`

## Query Parameters

This endpoint does not accept any query parameters.

## Response Shape

```json
{
  "statusCode": 200,
  "data": {
    "complaintsCount": 12,
    "featureRequestsCount": 7,
    "readFeedbacksCount": 15,
    "unreadFeedbacksCount": 4
  },
  "message": null
}
```

## Response Fields

| Field | Type | Description |
|---|---|---|
| `complaintsCount` | int | Total number of feedback items whose type is `Complaint` (`شكوى`). |
| `featureRequestsCount` | int | Total number of feedback items whose type is `FeatureRequest` (`اقتراح ميزة`). |
| `readFeedbacksCount` | int | Total number of feedback items where `isRead = true`. |
| `unreadFeedbacksCount` | int | Total number of feedback items where `isRead = false`. |

## Card Mapping For Frontend

Use the response fields for the four cards in this order:

1. `complaintsCount` for `الشكاوي`
2. `featureRequestsCount` for `اقتراح ميزة`
3. `readFeedbacksCount` for read feedbacks
4. `unreadFeedbacksCount` for unread feedbacks

## Frontend Integration Notes

- Call this endpoint once when the feedbacks page loads.
- The endpoint is separate from the paginated feedback list endpoint.
- Use this endpoint only for the top statistics cards.
- Use `GET /api/Feedbacks` for the feedback table itself.
- No request body is needed.
- No query string is needed.
- The values are already aggregated by the backend and can be displayed directly.

## Suggested Frontend Requests

### Cookie-based dashboard request

```http
GET /api/Feedbacks/statistics
Cookie: access_token=<dashboard_auth_cookie>
```

### Bearer token request

```http
GET /api/Feedbacks/statistics
Authorization: Bearer <your_token>
```

## Example JavaScript

```javascript
const response = await fetch('/api/Feedbacks/statistics', {
  method: 'GET',
  credentials: 'include'
});

const result = await response.json();

const stats = result.data;

const cards = {
  complaints: stats.complaintsCount,
  featureRequests: stats.featureRequestsCount,
  read: stats.readFeedbacksCount,
  unread: stats.unreadFeedbacksCount
};
```

## Notes For Another Model

If another model is integrating this page, it should assume:

- this endpoint is only for the feedback statistics cards
- this endpoint does not return the feedback list
- the feedback list should still come from `GET /api/Feedbacks`
- the four returned values are direct totals and do not need extra client-side calculation
- the user must be authenticated as `Admin` or `SubAdmin`
