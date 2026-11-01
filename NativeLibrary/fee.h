#ifndef FEE_H
#define FEE_H

#ifdef _WIN32
#define EXPORT __declspec(dllexport)
#else
#define EXPORT
#endif

EXPORT int CalculateNativeFee(int baseFee, int distanceKm);

#endif