<template>
  <div class="editor">
    <h2>{{ team.url ? "Edit" : "New" }} team</h2>

    <label class="input-header">Team name</label>
    <div> <input type='text' v-model="team.name" /></div> 

    <label class="input-header">Type</label>
    <label><input type="radio" :value="ContextType.Course" v-model="team.contexts[0].type" /> Create a team for the entire course</label>
    <label v-if="course.sections.length > 1"><input type="radio" :value="ContextType.Section" v-model="team.contexts[0].type" @input="team.createSectionChannels = false" /> Create a team for one or more sections</label>
    
    <div v-if="team.contexts[0].type == ContextType.Section">
      <label class="input-header">Sections to link to this team</label>
      <label v-for="sec in sections" :key="sec.name">
        <input type="checkbox" v-model="sec.checked" /> {{ sec.name }}
      </label>
    </div>

    <label class="input-header" v-if="selectedSections.length > 1 || course.groupSets.length > 0">Create private channels ({{ channelCount }}/30)</label>
    <label v-if="selectedSections.length > 1">
      <input type="checkbox" v-model="team.createSectionChannels" /> For each section 
    </label>
    <label v-for="set in groupSets" :key="set.name">
        <input type="checkbox" v-model="set.checked" />
        For each group in the category {{ set.name }} 
    </label>

    <label class="input-header">Options</label>
    <label><input type="checkbox" v-model="team.allowChannels" /> Allow users to create channels </label>
    <label><input type="checkbox" v-model="team.allowPrivateChannels" /> Allow users to create private channels </label>

    <button @click="save" :disabled="(team.contexts[0].type == ContextType.Section && sections.filter(s => s.checked).length == 0) || team.name.trim().length < 2">{{ team.url ? "Update team" : "Create team" }}</button>
    <button @click="$emit('close')" class="button-secondary">Cancel</button>
  </div>
  <LoadingScreen v-if="isSaving" />
</template>

<script lang="ts">
import { Options, Vue } from 'vue-class-component';
import { ContextType, CourseInfo, Section, Team } from '@/models/CourseInfo';
import LoadingScreen from './LoadingScreen.vue';
import GroupSetWrapper from '@/models/GroupSetWrapper';
import SectionWrapper from '@/models/SectionWrapper';
import axios from 'axios';

@Options({
  props: {
    course: Object,
    team: Object
  },
  components: { LoadingScreen }
})
export default class TeamEditor extends Vue {
  team!: Team;
  course!: CourseInfo;
  ContextType = ContextType;

  sections!: SectionWrapper[];
  groupSets!: GroupSetWrapper[];

  isSaving = false;

  created(): void {
    this.sections = this.course.sections.map(s => new SectionWrapper(this.team, s));
    this.groupSets = this.course.groupSets.map(s => new GroupSetWrapper(this.team, s));
  }

  save(): void {
    if (this.channelCount > 30) {
      alert('Too many private channels');
      return;
    }
    this.isSaving = true;
    if (this.team.contexts[0].type == ContextType.Section && this.team.contexts[0].id == -1) {
      this.team.contexts = this.team.contexts.slice(1);
    }

    if (!this.team.url) {
      if (this.team.createSectionChannels) {
        for (let sec of this.selectedSections) {
          this.team.channels.push({
            name: sec.name,
            contexts: [{ type: ContextType.Section, id: sec.id }]
          });
        }
      }

      this.course.teams.push(this.team);
      this.team.url = 'test';
    }
    axios.post(process.env.VUE_APP_ENDPOINT + '/CourseInfo', this.team).then(() => {
      this.isSaving = false;
      this.$emit('close');
    });
  }

  get selectedSections(): Section[] {
    return this.team.contexts[0].type == ContextType.Course ? this.course.sections 
      : this.sections.filter(s => s.checked).map(s => s.section);
  }

  get channelCount(): number {
    return (this.team.createSectionChannels ? this.selectedSections.length : 0) 
      + this.groupSets.filter(g => g.checked).map(g => g.set.groupCount).reduce((a,b) => a+b, 0);
  }
}
</script>

<style scoped lang="scss">
    .editor {
      font-size: 14px;
    }

    label {
        display: block;

        &.input-header {
          font-size: 16px;
            margin-top: 15px;
            margin-bottom: 3px;
        }
    }

    button {
        margin-top: 20px;
    }
</style>
