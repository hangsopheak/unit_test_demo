import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, HEADERS, randomOrder } from './helpers/config.js';

// Smoke test: 1 user, 30 seconds — sanity check before real testing
export const options = {
  vus: 1,
  duration: '30s',
  thresholds: {
    http_req_duration: ['p(95)<500'],  // 95% of requests under 500ms
    http_req_failed: ['rate<0.01'],    // Less than 1% errors
  },
};

export default function () {
  // Create an order
  const createRes = http.post(
    `${BASE_URL}/api/orders`,
    randomOrder(),
    { headers: HEADERS }
  );

  check(createRes, {
    'create: status is 201': (r) => r.status === 201,
    'create: has id': (r) => r.json('id') !== undefined,
  });

  sleep(1); // Think time — real users don't click instantly
}
