/// D depends on the cycle (D -> A) but is not a member of it.
/// Expected: higher topo_level than the SCC, distinct cycle_group_id,
/// status 'pending' until all of A/B/C are converted.
import 'A.dart';

class D {}
