import { validatePlaceDay } from './place-day';

const startsOn = '2027-04-01';
const endsOn = '2027-04-10';

describe('validatePlaceDay', () => {
  it('should accept a day inside the trip', () => {
    expect(validatePlaceDay(startsOn, endsOn, '2027-04-06', 'Fushimi Inari')).toBeNull();
  });

  it('should read both ends of the trip as days of the trip', () => {
    expect(validatePlaceDay(startsOn, endsOn, startsOn, 'Fushimi Inari')).toBeNull();
    expect(validatePlaceDay(startsOn, endsOn, endsOn, 'Fushimi Inari')).toBeNull();
  });

  it('should accept a place that has not been planned for a day', () => {
    expect(validatePlaceDay(startsOn, endsOn, null, 'Fushimi Inari')).toBeNull();
  });

  it('should refuse a day before the trip, naming the place and both days', () => {
    const message = validatePlaceDay(startsOn, endsOn, '2027-03-30', 'Fushimi Inari');

    expect(message).toContain('Fushimi Inari');
    expect(message).toContain('30 Mar 2027');
    expect(message).toContain('before the trip starts');
    expect(message).toContain('1 Apr 2027');
  });

  it('should refuse a day after the trip, naming the place and both days', () => {
    const message = validatePlaceDay(startsOn, endsOn, '2027-04-20', 'Fushimi Inari');

    expect(message).toContain('Fushimi Inari');
    expect(message).toContain('20 Apr 2027');
    expect(message).toContain('after the trip ends');
    expect(message).toContain('10 Apr 2027');
  });

  it('should refuse a day by a year, not only by a day', () => {
    expect(validatePlaceDay(startsOn, endsOn, '2028-04-06', 'Fushimi Inari')).toContain('after');
  });

  it('should not leave a blank name out of the sentence', () => {
    expect(validatePlaceDay(startsOn, endsOn, '2027-03-30', '   ')).toContain('A saved place');
  });

  it('should say nothing when the trip’s own dates are not dates', () => {
    // The API is the authority; this only says something when it can measure
    // the day against something it understands.
    expect(validatePlaceDay('', '', '2027-03-30', 'Fushimi Inari')).toBeNull();
  });
});
