# 기술 설계서 (Technical Design)

문서 버전: v0.1 (초안) · 관련 문서: [GDD.md](./GDD.md)

---

## 1. 엔진 및 기술 스택

| 항목 | 선택 | 이유 |
|---|---|---|
| 엔진 | **Unity 6 (LTS)**, 2D URP | 모바일 최적화 실적, Spine 공식 런타임, 2D 라이트/포스트프로세싱, 에셋 스토어 |
| 언어 | C# | |
| 애니메이션 | Spine (spine-unity) | 고퀄 2D 캐릭터·보스 |
| 데이터 | ScriptableObject + CSV/JSON 임포트 | 기획자가 스프레드시트로 밸런스 수정 |
| 리소스 로딩 | Addressables | 챕터 단위 에셋 로드/해제, 추후 패치 다운로드 |
| 대량 처리 | 오브젝트 풀링 → (필요 시) Jobs + Burst | 적 300~500마리 60fps |
| 세이브 | 로컬 JSON(암호화) → 추후 서버 동기화 | |
| 테스트 | Unity Test Framework (EditMode: 공식·계산 / PlayMode: 스폰·전투) | |

> 대안: Godot 4 (가볍고 무료, 다만 Spine·모바일 생태계가 상대적으로 약함). 웹 우선이라면 Phaser/PixiJS.

---

## 2. 전체 아키텍처

```
┌──────────────────────────────────────────────────────────┐
│                       GameManager                         │
│        (씬 전환, 게임 상태: Lobby / InRun / Result)        │
└───────┬───────────────────┬──────────────────┬───────────┘
        │                   │                  │
  ┌─────▼─────┐      ┌──────▼──────┐    ┌──────▼──────┐
  │  Meta 계층 │      │   Run 계층   │    │  공통 서비스 │
  │ (로비/육성)│      │ (전투 1판)   │    │             │
  ├───────────┤      ├─────────────┤    ├─────────────┤
  │ Profile   │      │ RunController│   │ SaveService │
  │ Roster    │─────▶│ PartyController│ │ DataTables  │
  │ Inventory │ 편성 │ WaveSpawner  │   │ PoolService │
  │ StatAlloc │ 전달 │ SkillSystem  │   │ AudioService│
  │ StageMap  │◀─────│ CombatSystem │   │ EventBus    │
  └───────────┘ 보상 │ BossController│  └─────────────┘
                     │ DropSystem   │
                     └─────────────┘
```

- **Meta → Run**: 출전 파티(캐릭터 + 최종 스탯 + 장비)를 `RunLoadout`으로 스냅샷 전달.
- **Run → Meta**: 결과(`RunResult`: 클리어 여부, 별, 획득 아이템/EXP)를 반환.
- 두 계층은 서로 직접 참조하지 않고 데이터 객체로만 통신 → 각각 독립 테스트 가능.

---

## 3. 핵심 데이터 모델

```csharp
// ── 정적 데이터 (ScriptableObject) ──
class CharacterDef   { id, name, classType, baseStats, growthPerLevel, startWeaponSkillId, spineAsset }
class EquipmentDef   { id, slot, rarity, baseMainStat, subStatPool, setId, uniqueEffectId, icon }
class SkillDef       { id, type(Active/Passive), maxLevel, levelValues[], evolveWith, evolvesTo, classRestriction }
class EnemyDef       { id, hp, atk, def, speed, behavior, expDrop, prefab }
class StageDef       { id, chapter, isBoss, duration, waveTimeline[], bossId, rewards }
class WaveEntry      { time, enemyId, count, pattern(Random/Circle/Line), interval }

// ── 동적 데이터 (세이브 대상) ──
class PlayerProfile  { gold, gems, stamina, clearedStage, stars{}, ownedCharacters[], inventory[], partyIds[3] }
class CharacterSave  { defId, level, exp, allocatedStats{STR,DEX,INT,VIT,LUK}, unspentPoints, equipped{slot→itemUid} }
class ItemInstance   { uid, defId, enhanceLevel, subStats[] }
```

### 3.1 스탯 계산 파이프라인
```
최종 스탯 = (직업 기본 + 레벨 성장 + 분배 포인트)          ← 기본 스탯(STR 등)
          → 파생 스탯 변환 (STR→공격력 등)
          → + 장비 주옵션/부옵션 (고정값 → % 순서)
          → + 세트 효과 + 파티 시너지
          → + 런 중 패시브 스킬 버프 (Run 계층에서만)
```
- `StatBlock` 구조체 + `StatModifier { stat, type(Flat/Percent), value, source }` 리스트 방식.
- Meta 단계 계산 결과는 캐시하고, 장비/스탯 변경 시에만 재계산.

---

## 4. Run(전투) 계층 상세

### 4.1 파티 컨트롤러
- `PartyController`가 리더 1명 + 팔로워 0~2명 관리.
- 팔로워 이동: 리더 기준 **포메이션 오프셋**(삼각형 배치) + 스티어링(분리/추종)으로 자연스럽게 따라감.
- 리더 교체: 입력 → 카메라 타깃·조작 대상 전환, 쿨타임 관리.
- 다운/부활 상태 머신: `Alive → Downed(타이머) → Alive` / 전원 Downed → `RunFailed` 이벤트.

### 4.2 대량 적 처리 (성능 핵심)
| 문제 | 해결 |
|---|---|
| 생성/파괴 비용 | 적·투사체·경험치 보석·데미지 텍스트 전부 **오브젝트 풀링** |
| 충돌 판정 비용 | Unity 물리 대신 **공간 해시 그리드(Spatial Hash)** 로 근접 탐색 |
| 적끼리 겹침 | 그리드 기반 간단한 분리(Separation) 힘, 매 프레임이 아닌 분산 업데이트 |
| 드로우콜 | 스프라이트 아틀라스 + GPU Instancing, 적은 Spine 대신 스프라이트 시트 |
| 업데이트 비용 | 적 이동은 매니저 1곳에서 일괄 처리(개별 MonoBehaviour.Update 금지) → 필요 시 Jobs/Burst로 이관 |
| 화면 밖 적 | 애니메이션/렌더 정지, 로직만 저빈도 업데이트 |

### 4.3 웨이브 스포너
- `StageDef.waveTimeline`을 시간순으로 읽어 스폰.
- 스폰 위치: 카메라 뷰포트 바깥 링 영역(패턴: 랜덤/원형 포위/직선 행렬).
- 동시 적 수 상한(예: 400)을 넘으면 스폰 지연 → 성능 보호.

### 4.4 스킬 시스템
- 스킬 = `SkillDef`(데이터) + `SkillBehaviour`(실행 로직: 투사체/오라/궤도/소환 등 타입별 클래스).
- 레벨업 카드 추첨: 가중치 기반, 보유 슬롯·최대 레벨·진화 조건·직업 제한 필터링.
- 진화 판정: 상자 획득 시 조건 충족 스킬 진화.

### 4.5 보스
- `BossController` = 페이즈별 패턴 리스트 + 상태 머신(Idle → Telegraph → Attack → Recover).
- 패턴은 데이터화(ScriptableObject)하여 보스 추가 시 코드 수정 최소화.
- 아레나: 보스 등장 시 원형 경계 콜라이더 + 이펙트.

### 4.6 이벤트 버스
`OnEnemyKilled`, `OnExpGained`, `OnRunLevelUp`, `OnMemberDowned`, `OnBossPhaseChanged`, `OnRunEnd` 등을 발행 → UI·사운드·업적이 구독 (시스템 간 결합도 감소).

---

## 5. Meta(육성) 계층 상세

| 화면 | 기능 |
|---|---|
| 로비 | 파티 3슬롯 미리보기, 스테이지 시작 |
| 영웅 | 캐릭터 목록, 레벨/스탯 분배(+/- 버튼, 확정, 초기화, 추천) |
| 장비 | 슬롯별 착용/해제, 비교 툴팁, 강화/합성/분해 |
| 스테이지 맵 | 챕터별 스테이지 노드, 별 보상, 보스 노드 강조 |
| 여관 | 동료 영입 |

- 스탯 분배는 "임시 분배 → 확정" 2단계로 실수 방지.

---

## 6. 세이브 & 서버
- 1단계: 로컬 저장(JSON + AES 암호화 + 체크섬), 스테이지 종료·장비 변경 시 자동 저장.
- 2단계(라이브 서비스 시): 서버 권위 방식으로 보상 검증 (치팅 방지), 클라우드 세이브.

---

## 7. 프로젝트 폴더 구조 (예정)
```
Assets/
  _Project/
    Scripts/
      Core/        (GameManager, EventBus, Pool, Save)
      Data/        (Def 클래스, 테이블 로더)
      Meta/        (Profile, Inventory, StatAllocation, UI)
      Run/
        Party/
        Enemies/
        Skills/
        Boss/
        Spawning/
      UI/
    Data/          (ScriptableObject 에셋, CSV 원본)
    Art/
      Characters/ Enemies/ Bosses/ Environment/ UI/ VFX/
    Audio/
    Scenes/        (Boot, Lobby, Run)
  Tests/
    EditMode/  PlayMode/
docs/
```

---

## 8. 개발 로드맵

| 마일스톤 | 목표 | 주요 산출물 |
|---|---|---|
| **M0. 프로토타입** (2~3주) | 핵심 재미 검증 | 캐릭터 1명 이동·자동공격, 적 스폰, 경험치·런 레벨업 스킬 3종, 임시 그래픽 |
| **M1. 파티 & 성능** (3주) | 3인 파티, 적 400마리 60fps | 팔로워 AI, 리더 교체, 공간 해시, 풀링 |
| **M2. 메타 성장** (3주) | 육성 루프 완성 | 캐릭터 레벨·스탯 분배, 장비 6슬롯·등급·강화, 세이브 |
| **M3. 챕터 1 버티컬 슬라이스** (4주) | 고퀄 아트 적용 1챕터 완성 | 스테이지 1~10, 보스 "썩은뿌리", 최종 아트·이펙트·사운드 |
| **M4. 콘텐츠 확장** | 챕터 2~5 | 신규 직업·적·보스, 진화 스킬 |
| **M5. 출시 준비** | 폴리싱/QA | 튜토리얼, 최적화, 스토어 빌드 |

---

## 9. 기술 리스크
| 리스크 | 대응 |
|---|---|
| 고퀄 아트 + 대량 적 → 모바일 성능 저하 | M1에서 저사양 기기 기준 성능 목표 먼저 확보, 적은 경량 스프라이트 사용 |
| 파티 3인 + 수백 투사체 → 화면 가독성 저하 | 이펙트 색 규칙, 팔로워 이펙트 투명도 하향 옵션 |
| 스탯·장비·스킬 곱연산 → 밸런스 붕괴 | 데이터 테이블화 + 시뮬레이션 스크립트(DPS/생존시간 계산) |
| 아트 제작량 과다 | 챕터 1에서 파이프라인 확정 후 템플릿화, 몬스터 팔레트 스왑 활용 |
