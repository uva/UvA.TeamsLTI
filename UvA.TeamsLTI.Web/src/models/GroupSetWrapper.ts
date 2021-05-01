import { GroupSet, Team } from "./CourseInfo";

export default class GroupSetWrapper {
    team: Team;
    set: GroupSet;

    get name(): string { return this.set.name; }

    constructor(team: Team, set: GroupSet) {
        this.team = team;
        this.set = set;
    }

    get checked(): boolean { return this.team.groupSetIds.includes(this.set.id); }

    set checked(val: boolean) {
        if (val && !this.checked) {
            this.team.groupSetIds.push(this.set.id);
        } else if (!val) {
            this.team.groupSetIds = this.team.groupSetIds.filter(i => i != this.set.id);
        }
    }
}