import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { BASE_URL, HEADERS, randomOrder } from './helpers/config.js';

// Full workflow: Create → Get → Calculate Fee → Delete
// Tests the complete user journey under load
export const options = {
  stages: [
    { duration: '30s', target: 20 },
    { duration: '1m',  target: 20 },
    { duration: '30s', target: 0 },
  ],
  thresholds: {
    http_req_duration: ['p(95)<800'],
    http_req_failed: ['rate<0.05'],
  },
};

export default function () {
  let orderId;

  // Step 1: Create order
  group('01_create_order', () => {
    const res = http.post(
      `${BASE_URL}/api/orders`,
      randomOrder(),
      { headers: HEADERS }
    );

    const success = check(res, {
      'create: 201': (r) => r.status === 201,
    });

    if (success) {
      orderId = res.json('id');
    }
  });

  if (!orderId) return; // Skip remaining steps if create failed

  sleep(1);

  // Step 2: Get the order back
  group('02_get_order', () => {
    const res = http.get(`${BASE_URL}/api/orders/${orderId}`);

    check(res, {
      'get: 200': (r) => r.status === 200,
      'get: correct id': (r) => r.json('id') === orderId,
    });
  });

  sleep(0.5);

  // Step 3: Calculate delivery fee
  group('03_calculate_fee', () => {
    const res = http.post(`${BASE_URL}/api/orders/${orderId}/calculate-fee`);

    check(res, {
      'fee: 200': (r) => r.status === 200,
      'fee: has deliveryFee': (r) => r.json('deliveryFee') !== undefined,
      'fee: has total': (r) => r.json('total') !== undefined,
    });
  });

  sleep(0.5);

  // Step 4: Delete the order (cleanup)
  group('04_delete_order', () => {
    const res = http.del(`${BASE_URL}/api/orders/${orderId}`);

    check(res, {
      'delete: 204': (r) => r.status === 204,
    });
  });

  sleep(1);
}
