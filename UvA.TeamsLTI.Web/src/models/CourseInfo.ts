export interface CourseInfo {
    id: number,
    name: string,
    sections: Section[],
    groupSets: GroupSet[],
    teams: Team[]
}

export interface GroupSet {
    id: number;
    name: string;
    groupCount: number;
}

export interface Section {
    id: number;
    name: string;
}

export interface Team {
    id?: string;
    name: string;
    contexts: Context[];
    channels: Channel[];
    url?: string;

    allowChannels: boolean;
    allowPrivateChannels: boolean;
    
    createSectionChannels: boolean;
    groupSetIds: number[];
}

export interface Channel {
    name: string;
    contexts: Context[];
}

export interface Context {
    id: number;
    type: ContextType;
}

export enum ContextType {
    Course,
    Section,
    Group,
    GroupSet
}