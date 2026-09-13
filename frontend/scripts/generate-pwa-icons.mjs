// One-off script to (re)generate PWA icons from public/logo/mark.png.
// Run with: node scripts/generate-pwa-icons.mjs
// Requires `sharp` installed locally (npm install --no-save sharp), it is
// not a project dependency since it's only needed when regenerating icons.
import sharp from 'sharp';
import { mkdirSync } from 'node:fs';

const SOURCE = 'public/logo/mark.png';
const OUT_DIR = 'public/icons';
const THEME_COLOR = '#087f72';
const SIZES = [72, 96, 128, 144, 152, 192, 384, 512];

mkdirSync(OUT_DIR, { recursive: true });

for (const size of SIZES) {
  await sharp(SOURCE)
    .resize(size, size, { fit: 'contain', background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .png()
    .toFile(`${OUT_DIR}/icon-${size}x${size}.png`);
}

// Maskable icon: logo scaled to ~60% and centered on a solid background,
// so OS-applied circular/rounded-square masks don't clip the artwork.
const maskableSize = 512;
const logoSize = Math.round(maskableSize * 0.6);
const offset = Math.round((maskableSize - logoSize) / 2);
const logoBuffer = await sharp(SOURCE).resize(logoSize, logoSize, { fit: 'contain' }).toBuffer();
await sharp({
  create: {
    width: maskableSize,
    height: maskableSize,
    channels: 4,
    background: THEME_COLOR,
  },
})
  .composite([{ input: logoBuffer, left: offset, top: offset }])
  .png()
  .toFile(`${OUT_DIR}/maskable-icon-512x512.png`);

// Apple touch icon: iOS ignores alpha, so flatten onto a solid background.
await sharp(SOURCE)
  .resize(180, 180, { fit: 'contain', background: THEME_COLOR })
  .flatten({ background: THEME_COLOR })
  .png()
  .toFile(`${OUT_DIR}/apple-touch-icon.png`);

console.log('PWA icons generated in', OUT_DIR);
