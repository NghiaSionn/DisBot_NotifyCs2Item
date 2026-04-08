const { spawn } = require('child_process');
const { chmodSync } = require('fs');

console.log("[Node.js Wrapper] Đang chuẩn bị môi trường chạy bot C#...");

try {
  // Cấp quyền thực thi động cho file nhị phân Linux
  chmodSync('./CS2PriceBot', '777');
} catch (error) {
  console.log("[Node.js Wrapper] Cảnh báo khi cấp quyền: ", error.message);
}

// Khởi chạy file thực thi C# đã được biên dịch cho Linux
const botProcess = spawn('./CS2PriceBot', [], { stdio: 'inherit' });

botProcess.on('close', (code) => {
  console.log(`[Node.js Wrapper] Bot C# đã dừng với mã ${code}`);
  process.exit(code);
});

botProcess.on('error', (err) => {
  console.error('[Node.js Wrapper] Lỗi khi chạy file CS2PriceBot:', err);
});
