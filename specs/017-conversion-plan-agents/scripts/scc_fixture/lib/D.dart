/// D depends on the SCC (D -> A) but is not a member of it.
/// Feature 017 US3 / SC-006: D is NOT plan-ready until ALL of A/B/C
/// have a completed plan; distinct cycle_group_id, higher topo_level.
import 'A.dart';

class D {}
