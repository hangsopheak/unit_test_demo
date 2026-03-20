import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, HEADERS, randomOrder } from './helpers/config.js';

// Spike test: 0 → 300 users in 10 seconds — sudden traffic burst
export const options = {
  stages: [
    { duration: '10s', target: 10 },   // Normal traffic
    { duration: '10s', target: 300 },  // SPIKE — the flash sale begins
    { duration: '30s', target: 300 },  // Sustained spike
    { duration: '10s', target: 10 },   // Spike over — does it recover?
    { duration: '30s', target: 10 },   // Recovery period
  ],
  thresholds: {
    http_req_duration: ['p(95)<3000'], // Very relaxed — spike conditions
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
  });

  sleep(0.3); // Minimal think time — everyone clicking at once
}
