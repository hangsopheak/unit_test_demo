import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, HEADERS, randomOrder } from './helpers/config.js';

// Soak test: sustained load for extended duration — finds memory leaks
// NOTE: Real soak tests run 1-4 hours. This is shortened for demo (10 min).
export const options = {
  stages: [
    { duration: '1m',  target: 30 },  // Ramp up
    { duration: '8m',  target: 30 },  // Sustained load — look for degradation
    { duration: '1m',  target: 0 },   // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.05'],
  },
};

export default function () {
  // Create order — adds data to the DB over time
  const createRes = http.post(
    `${BASE_URL}/api/orders`,
    randomOrder(),
    { headers: HEADERS }
  );

  check(createRes, {
    'create: status is 201': (r) => r.status === 201,
  });

  // List orders — this gets SLOWER as the DB grows (no pagination!)
  const listRes = http.get(`${BASE_URL}/api/orders`);

  check(listRes, {
    'list: status is 200': (r) => r.status === 200,
  });

  sleep(1);
}
