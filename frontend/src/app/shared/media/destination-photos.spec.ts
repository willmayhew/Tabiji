import { resolveDestinationPhoto } from './destination-photos';

describe('resolveDestinationPhoto', () => {
  it('should resolve a known destination to its photograph', () => {
    const photo = resolveDestinationPhoto('Kyoto');

    expect(photo?.slug).toBe('kyoto');
    expect(photo?.src).toBe('/photos/kyoto-1280.webp');
    expect(photo?.srcSet).toContain('/photos/kyoto-640.webp 640w');
    expect(photo?.width).toBe(1280);
    expect(photo?.height).toBe(853);
  });

  it('should ignore the case of the destination', () => {
    expect(resolveDestinationPhoto('TOKYO')?.slug).toBe('tokyo');
    expect(resolveDestinationPhoto('tokyo')?.slug).toBe('tokyo');
  });

  it('should trim surrounding whitespace', () => {
    expect(resolveDestinationPhoto('  Osaka  ')?.slug).toBe('osaka');
  });

  it('should prefer a named place over the country', () => {
    expect(resolveDestinationPhoto('Kyoto, Japan')?.slug).toBe('kyoto');
    expect(resolveDestinationPhoto('Japan, Kyoto')?.slug).toBe('kyoto');
  });

  it('should prefer the first named place that is mentioned', () => {
    expect(resolveDestinationPhoto('Tokyo and Kyoto')?.slug).toBe('tokyo');
    expect(resolveDestinationPhoto('Kyoto and Tokyo')?.slug).toBe('kyoto');
  });

  it('should fall back to the country photograph', () => {
    expect(resolveDestinationPhoto('Japan')?.slug).toBe('japan');
    expect(resolveDestinationPhoto('Japan')?.alt).toContain('Japanese garden');
    expect(resolveDestinationPhoto('Japanese Alps')?.slug).toBe('japan');
  });

  it('should match a place written in Japanese', () => {
    expect(resolveDestinationPhoto('京都')?.slug).toBe('kyoto');
    expect(resolveDestinationPhoto('東京')?.slug).toBe('tokyo');
  });

  it('should match a keyword on word boundaries only', () => {
    expect(resolveDestinationPhoto('Nara')?.slug).toBe('nara');
    expect(resolveDestinationPhoto('Nara, Japan')?.slug).toBe('nara');
    expect(resolveDestinationPhoto('Nararino')).toBeNull();
  });

  it('should resolve a destination that names Mount Fuji', () => {
    expect(resolveDestinationPhoto('Mount Fuji')?.slug).toBe('fuji');
  });

  it('should not resolve a region', () => {
    expect(resolveDestinationPhoto('Kanto')).toBeNull();
    expect(resolveDestinationPhoto('Kansai')).toBeNull();
  });

  it('should not resolve an unknown destination', () => {
    expect(resolveDestinationPhoto('Reykjavik')).toBeNull();
  });

  it('should not resolve an empty destination', () => {
    expect(resolveDestinationPhoto('')).toBeNull();
    expect(resolveDestinationPhoto('   ')).toBeNull();
  });

  it('should not resolve a missing destination', () => {
    expect(resolveDestinationPhoto(null)).toBeNull();
    expect(resolveDestinationPhoto(undefined)).toBeNull();
  });

  it('should give every photograph an alt description', () => {
    const destinations = ['Kyoto', 'Tokyo', 'Osaka', 'Nara', 'Hakone', 'Japan', 'Hiroshima'];

    for (const destination of destinations) {
      expect(resolveDestinationPhoto(destination)?.alt.length).toBeGreaterThan(10);
    }
  });
});
