import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, HEADERS, randomOrder } from './helpers/config.js';

// Load test: ramp to 50 users — simulates expected peak traffic
export const options = {
  stages: [
    { duration: '30s', target: 20 },  // Ramp up to 20 users
    { duration: '1m',  target: 50 },  // Ramp to peak: 50 users
    { duration: '30s', target: 50 },  // Hold at peak
    { duration: '30s', target: 0 },   // Ramp down (graceful)
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'],  // SLO: 95th percentile < 500ms
    http_req_failed: ['rate<0.05'],    // SLO: error rate < 5%
  },
};

export default function () {
  // Create order
  const createRes = http.post(
    `${BASE_URL}/api/orders`,
    randomOrder(),
    { headers: HEADERS }
  );

  check(createRes, {
    'create: status is 201': (r) => r.status === 201,
  });

  // List orders
  const listRes = http.get(`${BASE_URL}/api/orders`);

  check(listRes, {
    'list: status is 200': (r) => r.status === 200,
  });

  sleep(1);
}
