import { requireThat } from './model.mjs';

export async function boundedResponse(response, max = 10_000_000) {
  requireThat(response.ok, 'Remote read failed');
  const chunks = []; let length = 0;
  for await (const chunk of response.body) {
    length += chunk.length;
    if (length > max) { throw new Error('Remote response exceeds limit'); }
    chunks.push(chunk);
  }
  return Buffer.concat(chunks);
}
export class GitHubReader {
  constructor(token, fetcher = fetch) { this.token = token; this.fetcher = fetcher; }
  async response(endpoint) {
    requireThat(/^repos\/von12549\/ArchSift\//.test(endpoint) && !endpoint.includes('..'), 'Unapproved GitHub endpoint');
    return this.fetcher(`https://api.github.com/${endpoint}`, { headers: { Authorization: `Bearer ${this.token}`, Accept: 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28' }, redirect: 'manual', signal: AbortSignal.timeout(120_000) });
  }
  async json(endpoint) { return JSON.parse((await boundedResponse(await this.response(endpoint), 16_000_000)).toString('utf8')); }
  async blob(sha) {
    requireThat(/^[a-f0-9]{40}$/.test(sha), 'Invalid blob SHA');
    const b = await this.json(`repos/von12549/ArchSift/git/blobs/${sha}`);
    requireThat(b.encoding === 'base64' && b.size <= 10_000_000, 'Blob encoding/size differs');
    const bytes = Buffer.from(b.content, 'base64');
    requireThat(bytes.length === b.size, 'Blob length differs'); return bytes;
  }
  async artifact(id) {
    requireThat(Number.isSafeInteger(id) && id > 0, 'Invalid artifact ID');
    const response = await this.response(`repos/von12549/ArchSift/actions/artifacts/${id}/zip`);
    requireThat(response.status === 302, 'Artifact download redirect missing');
    const url = new URL(response.headers.get('location'));
    requireThat(url.protocol === 'https:' && !url.username && !url.password && (url.hostname.endsWith('.blob.core.windows.net') || url.hostname.endsWith('.actions.githubusercontent.com')), 'Unapproved artifact storage');
    // Never send GitHub authorization to storage, or log its signed URL.
    return boundedResponse(await this.fetcher(url, { redirect: 'error', signal: AbortSignal.timeout(120_000) }));
  }
}
