import { ContextType, Section, Team } from "./CourseInfo";

export default class SectionWrapper {
    team: Team;
    section: Section;
  
    get name(): string { return this.section.name; }
  
    get checked(): boolean {
      return this.team.contexts.filter(s => s.type == ContextType.Section && s.id == this.section.id).length > 0;
    }
  
    set checked(val: boolean) {
      if (val && !this.checked) {
        this.team.contexts.push({ type: ContextType.Section, id: this.section.id });
      } else if (!val) {
        this.team.contexts = this.team.contexts.filter(f => f.type != ContextType.Section || f.id != this.section.id);
        if (this.team.contexts.length == 0) {
            this.team.contexts.push({ type: ContextType.Course, id: -1 });
        }
      }
    }
  
    constructor(team: Team, sec: Section) {
      this.team = team;
      this.section = sec;
    }
  }