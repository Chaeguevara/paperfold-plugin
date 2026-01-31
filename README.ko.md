# paperfold-plugin

[English](README.md) | [한국어](README.ko.md)

Rhino 8용 Grasshopper 플러그인으로 나선형 지오메트리를 생성합니다.

## 컴포넌트

### 아르키메데스 나선 (Archimedean Spiral)

내부 반지름과 외부 반지름 사이에서 반원호를 연결하여 나선을 생성합니다.

| 파라미터 | 타입 | 설명 |
|---------|------|------|
| **Plane** (P) | Plane | 기준 평면 (기본값: World XY) |
| **Inner Radius** (R0) | Number | 시작 반지름 (기본값: 1.0) |
| **Outer Radius** (R1) | Number | 끝 반지름 (기본값: 10.0) |
| **Turns** (T) | Integer | 반회전 수 (기본값: 10) |

**출력:** Spiral (S) -- 하나의 `PolyCurve`로 결합된 나선.

### 피보나치 나선 (Fibonacci Spiral)

피보나치 수열(1, 1, 2, 3, 5, 8, 13, ...)을 변의 길이로 하는 정사각형을
타일링하고, 각 정사각형 안에 사분원호를 그립니다. 연결된 호들이 황금 나선을
근사합니다.

| 파라미터 | 타입 | 설명 |
|---------|------|------|
| **Steps** (N) | Integer | 피보나치 단계 수 (기본값: 10, 최대: 50) |

**출력:**

| 출력 | 설명 |
|------|------|
| Rectangles (R) | 황금 직사각형 (닫힌 폴리라인) |
| Arcs (A) | 개별 사분원호 |
| Spiral (S) | 모든 호를 결합한 단일 곡선 |

#### 기술 노트

- 피보나치 수는 비네 공식(Binet's formula) 대신 반복법으로 계산하여 모든
  단계에서 정확한 정수 정밀도를 유지합니다.
- 회전축은 삼각함수 대신 정확한 벡터 룩업 테이블을 사용하여
  `cos(pi/2) = 6.12e-17` 같은 부동소수점 오차를 방지합니다.
- 호 결합에는 `Curve.JoinCurves` 대신 `PolyCurve.Append`를 사용합니다.
  대규모 지오메트리(F(47)이 30억 단위 초과)에서 허용 오차 기반
  결합이 실패하는 문제를 회피합니다.

## 요구 사항

- Rhino 8
- .NET SDK 7.0+

## 빌드

```
dotnet build
```

출력 경로: `bin/Debug/net7.0/ghFolding.gha`

대상 프레임워크: `net48`, `net7.0`, `net7.0-windows`.

## 설치

빌드된 `ghFolding.gha` 파일을 Grasshopper Libraries 폴더에 복사하거나,
VSCode에서 F5로 실행하면 `RHINO_PACKAGE_DIRS`가 자동 설정됩니다.

## 라이선스

MIT
