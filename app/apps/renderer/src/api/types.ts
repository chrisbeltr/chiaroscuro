// Hand-written mirror of Chiaroscuro.Api's Contracts/*.cs DTOs (camelCase, matching ASP.NET
// Core's default JSON naming policy). Per the migration plan these should eventually be
// generated from the backend's /openapi/v1.json via openapi-typescript instead of maintained
// by hand - this file is the interim, and any change to a Contracts/*.cs record must be
// mirrored here until that generation step exists.

export type WallOrientation = 'North' | 'South' | 'East' | 'West';
export type RoomSurface = 'Floor' | 'NorthWall' | 'SouthWall' | 'EastWall' | 'WestWall';

export interface Vector3Dto {
  x: number;
  y: number;
  z: number;
}

export interface RoomDto {
  width: number;
  length: number;
  height: number;
  rotationDegrees: number;
}

export interface WindowDto {
  wall: WallOrientation;
  horizontalOffset: number;
  sillHeight: number;
  width: number;
  height: number;
}

export interface SunPositionDto {
  elevationDegrees: number;
  azimuthDegrees: number;
}

export interface LandingPatchDto {
  surface: RoomSurface;
  polygon: Vector3Dto[];
}

export interface IlluminationDto {
  surface: RoomSurface;
  centerPoint: Vector3Dto;
  illuminatedPolygon: Vector3Dto[];
  patches: LandingPatchDto[];
}

export interface IlluminationRequest {
  room: RoomDto;
  window: WindowDto;
  latitude: number;
  longitude: number;
  year: number;
  month: number;
  day: number;
  hour: number;
  minute: number;
  utcOffsetHours: number;
}

export interface IlluminationResponse {
  sunPosition: SunPositionDto;
  illumination: IlluminationDto | null;
}

export interface AlignmentsRequest {
  room: RoomDto;
  window: WindowDto;
  target: Vector3Dto;
  latitude: number;
  longitude: number;
  utcOffsetHours: number;
  year: number;
  month: number;
  day: number;
  toleranceDegrees: number;
  maxResults?: number;
}

export interface AlignmentMatchDto {
  year: number;
  month: number;
  day: number;
  hour: number;
  minute: number;
  elevationDegrees: number;
  azimuthDegrees: number;
  angleDifferenceDegrees: number;
}

export interface AlignmentsResponse {
  matches: AlignmentMatchDto[];
}

export interface GeolocationResponse {
  success: boolean;
  latitude: number | null;
  longitude: number | null;
  utcOffsetHours: number | null;
}
