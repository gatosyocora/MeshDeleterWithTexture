// ABが上向きのベクトルかどうか
bool isUpwardVector(float2 a, float2 b) {
    return b.y - a.y > 0;
}

// ABが下向きのベクトルかどうか
bool isDownwardVector(float2 a, float2 b) {
    return b.y - a.y < 0;
}

// ABとCDが平行なベクトル関係か
bool isParallel(float2 a, float2 b, float2 c, float2 d) {
    float ab = (b.y - a.y) / (b.x - a.x);
    float cd = (d.y - c.y) / (d.x - c.x);
    return ab == cd;
}

// 外積
float mycross(float2 vec1, float2 vec2) {
    return vec1.x * vec2.y - vec1.y * vec2.x;
}

// ABとCDが交わっているか
bool isCrossLine(float2 a, float2 b, float2 c, float2 d) {
    return mycross(b - a, c - a) * mycross(b - a, d - a) <= 0 &&
        mycross(d - c, a - c) * mycross(d - c, b - c) <= 0;
}

bool isCountUp(float2 a, float2 b, float2 c, float2 d) {
    return !isParallel(a, b, c, d) && // 平行ならカウントしない
        isCrossLine(a, b, c, d) && // 交差するならカウントする
        ((isUpwardVector(a, b) && c.y != a.y) || // 上向きベクトルの始点と重なるならカウントしない
            (isDownwardVector(a, b) && c.y != b.y)); // 下向きベクトルの終点と重なるならカウントしない
}

// 太さlineSizeのABの線分上にpが載っているかどうか
bool isOnLine(float2 a, float2 b, float2 p, float lineSize, float width)
{
    // a,bを垂直方向に移動させた4点の長方形の中にあるかで判定する
    float2 q = float2(p.x + width, p.y);

    // ABに直交するベクトルを正規化
    float2 ab = a - b;
    float2 invAB = ab.yx;
    float2 normalizedInvAB = normalize(invAB);

    // a,bを垂直方向に移動させた4点
    float2 p1 = a + normalizedInvAB * float2(1, -1) * lineSize;
    float2 p2 = a + normalizedInvAB * float2(-1, 1) * lineSize;
    float2 p3 = b + normalizedInvAB * float2(-1, 1) * lineSize;
    float2 p4 = b + normalizedInvAB * float2(1, -1) * lineSize;

    int count = 0;
    if (isCountUp(p1, p2, p, q)) count++;
    if (isCountUp(p2, p3, p, q)) count++;
    if (isCountUp(p3, p4, p, q)) count++;
    if (isCountUp(p4, p1, p, q)) count++;

    return  fmod(count, 2) == 1;
}