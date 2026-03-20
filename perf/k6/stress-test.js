import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, HEADERS, randomOrder } from './helpers/config.js';

// Stress test: push to 200 users — find the breaking point
export const options = {
  stages: [
    { duration: '30s', target: 50 },   // Normal load
    { duration: '30s', target: 100 },  // Beyond normal
    { duration: '30s', target: 150 },  // Pushing limits
    { duration: '30s', target: 200 },  // Breaking point?
    { duration: '30s', target: 0 },    // Recovery — does it come back?
  ],
  thresholds: {
    http_req_duration: ['p(95)<2000'], // Relaxed — we EXPECT degradation
    http_req_failed: ['rate<0.50'],    // Allow up to 50% errors — we're finding limits
  },
};

export default function () {
  const createRes = http.post(
    `${BASE_URL}/api/orders`,
    randomOrder(),
    { headers: HEADERS }
  );

  check(createRes, {
    'create: status is 201': (r) => r.status === 201,
    'create: response time < 1s': (r) => r.timings.duration < 1000,
  });

  sleep(0.5); // Shorter think time — stressed users click faster
}
