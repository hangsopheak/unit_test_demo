// Shared configuration for all K6 scripts
export const BASE_URL = 'http://localhost:5000';

export const HEADERS = {
  'Content-Type': 'application/json',
};

// Random order generator — different payload each iteration
const NAMES = ['Alice', 'Bob', 'Charlie', 'Dave', 'Eve', 'Frank', 'Grace', 'Hank'];

export function randomOrder() {
  return JSON.stringify({
    customerName: NAMES[Math.floor(Math.random() * NAMES.length)],
    cartSubtotal: Math.round((Math.random() * 80 + 5) * 100) / 100,   // $5 – $85
    distanceInKm: Math.round((Math.random() * 30 + 1) * 10) / 10,     // 1 – 31 km
    isRushHour: Math.random() < 0.33,                                   // ~33% rush hour
  });
}
