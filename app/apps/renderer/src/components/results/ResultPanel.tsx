import type { UseQueryResult } from '@tanstack/react-query';
import type { IlluminationResponse } from '../../api/types';

interface Props {
  illumination: UseQueryResult<IlluminationResponse>;
}

// Rebuilds MainViewModel.Recalculate()'s ResultText string from useIllumination()'s raw
// numeric response - that formatting is presentation and was deliberately left out of
// Chiaroscuro.Api's IlluminationResponse (see SolarEndpoints.cs's own comment on this).
export function ResultPanel({ illumination }: Props) {
  return (
    <section className="panel result-panel">
      <p>{describe(illumination)}</p>
    </section>
  );
}

function describe(query: UseQueryResult<IlluminationResponse>): string {
  if (!query.data) {
    return query.isError ? 'Enter all parameters to calculate.' : 'Calculating...';
  }

  const { sunPosition, illumination } = query.data;
  const header = `Sun elevation ${sunPosition.elevationDegrees.toFixed(1)}°, azimuth ${sunPosition.azimuthDegrees.toFixed(1)}°`;

  if (!illumination) {
    return `${header}\nNo surface is illuminated at this time.`;
  }

  const { surface, centerPoint } = illumination;
  return `${header}\nLight lands on ${surface} at (${centerPoint.x.toFixed(2)}, ${centerPoint.y.toFixed(2)}, ${centerPoint.z.toFixed(2)})`;
}
