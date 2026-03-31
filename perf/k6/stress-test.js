import http from 'k6/http';
import { check } from 'k6';
import { BASE_URL, HEADERS, randomOrder } from './helpers/config.js';

// Stress test: slow ramp to find the exact breaking point.
// K6 abortOnFail stops the test the moment p(95) crosses 500ms —
// the VU count at that moment IS the breaking point.
export const options = {
  stages: [
    { duration: '30s', target: 200 },   // Zone 1: should be healthy
    { duration: '60s', target: 2000 },  // Slow ramp — watch exactly where it breaks
    { duration: '30s', target: 0 },     // Recovery
  ],
  thresholds: {
    http_req_duration: [
      { threshold: 'p(95)<500' }, 
    ],
    http_req_failed: ['rate<0.05'],
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
    'create: response time < 2s': (r) => r.timings.duration < 2000,
  });
}
