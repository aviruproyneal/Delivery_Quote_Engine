#include "fee.h"

int CalculateNativeFee(int baseFee, int distanceKm) {
    return baseFee + (distanceKm * 10);
}