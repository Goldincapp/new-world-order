// Generates the story voiceover with ElevenLabs into vo/vo-0.mp3 ... vo/vo-4.mp3.
// PowerShell:  $env:ELEVENLABS_API_KEY="your-key"; node gen-vo.mjs
// Optional:    $env:VOICE_ID="..." to use a different voice (default: Adam, a deep narrator).
import fs from 'fs';

const key = process.env.ELEVENLABS_API_KEY;
if (!key) { console.error('Set ELEVENLABS_API_KEY first'); process.exit(1); }
const voice = process.env.VOICE_ID || 'pNInz6obpgDQGcFmaJgB';
const here = p => new URL(p, import.meta.url);

// Read the story straight from the demo so the audio always matches the on-screen text.
const html = fs.readFileSync(here('./world-strategy-demo.html'), 'utf8');
const INTRO = eval(html.match(/const INTRO=(\[[\s\S]*?\]\]);/)[1]);
const lines = INTRO.map(([title, body]) => `${title}. <break time="1.2s" /> ${body.replace(/<[^>]+>/g, '')}`);
lines.push('New World Order. <break time="1.5s" /> The land is yours to claim, build, and govern. <break time="0.8s" /> Prove you can hold it.');

fs.mkdirSync(here('./vo/'), { recursive: true });
for (const [i, text] of lines.entries()) {
  const r = await fetch(`https://api.elevenlabs.io/v1/text-to-speech/${voice}?output_format=mp3_44100_128`, {
    method: 'POST',
    headers: { 'xi-api-key': key, 'Content-Type': 'application/json', Accept: 'audio/mpeg' },
    body: JSON.stringify({
      text,
      model_id: 'eleven_multilingual_v2',
      voice_settings: { stability: 0.5, similarity_boost: 0.8, style: 0.3, use_speaker_boost: true },
    }),
  });
  if (!r.ok) { console.error(`Line ${i} failed: ${r.status} ${await r.text()}`); process.exit(1); }
  fs.writeFileSync(here(`./vo/vo-${i}.mp3`), Buffer.from(await r.arrayBuffer()));
  console.log(`wrote vo/vo-${i}.mp3`);
}
console.log('Done. Reload the demo and tap to begin.');
